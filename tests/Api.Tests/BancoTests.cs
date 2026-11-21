using Microsoft.EntityFrameworkCore;
using Npgsql;
using Dayspot.Api.Dados;

namespace Dayspot.Api.Tests;

// Teste de isolamento da 0.14 (guia §Seed e teste de isolamento). Os dados são
// gravados pelo dono das tabelas (MIGRATIONS_DATABASE_URL) e lidos pelo app_user
// (DATABASE_URL), como a API faz. Sem as duas variáveis, pula; no CI, falha.
public class IsolamentoTests
{
    // UUIDs próprios do teste, fora da faixa do seed (0.16), para os dois conviverem.
    private static readonly Guid TenantAlfa = Guid.Parse("f1111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantBeta = Guid.Parse("f2222222-2222-2222-2222-222222222222");
    private static readonly Guid UsuarioAlfa = Guid.Parse("fa000000-0000-0000-0000-000000000001");
    private static readonly Guid UsuarioBeta = Guid.Parse("fb000000-0000-0000-0000-000000000001");

    private static readonly string[] TabelasComRls = ["clientes", "servicos", "disponibilidades", "agendamentos", "notificacoes"];

    private static async Task ApagarDados(NpgsqlConnection dono)
    {
        // O CASCADE de tenants leva clientes, agendamentos, notificações etc.
        await dono.Exec("DELETE FROM tenants WHERE id IN ($1, $2)", TenantAlfa, TenantBeta);
        await dono.Exec("DELETE FROM usuarios WHERE id IN ($1, $2)", UsuarioAlfa, UsuarioBeta);
    }

    /// <summary>
    /// Uma linha em cada tabela com tenant_id, para o tenant dado. Na transação com
    /// set_config: com FORCE, nem o dono grava sem tenant (a não ser que seja
    /// superusuário).
    /// </summary>
    internal static async Task CriarDadosDoTenant(NpgsqlConnection dono, Guid tenant, Guid usuario, string slug)
    {
        await using var tx = await dono.ComTenant(tenant);
        await dono.Exec("INSERT INTO tenants (id, nome, slug, criado_por) VALUES ($1, $2, $2, $3)", tenant, slug, usuario);
        await dono.Exec("INSERT INTO memberships (usuario_id, tenant_id, papel) VALUES ($1, $2, 'administrador')", usuario, tenant);
        await dono.Exec("INSERT INTO clientes (tenant_id, nome) VALUES ($1, 'Cliente')", tenant);
        await dono.Exec("INSERT INTO servicos (tenant_id, nome, duracao_minutos, preco_centavos) VALUES ($1, 'Serviço', 60, 5000)", tenant);
        await dono.Exec("INSERT INTO disponibilidades (tenant_id, prestador_id, dia_semana, hora_inicio, hora_fim) VALUES ($1, $2, 1, '09:00', '18:00')", tenant, usuario);
        await dono.Exec("""
            INSERT INTO agendamentos (tenant_id, cliente_id, servico_id, prestador_id, inicio, fim, valor_centavos)
            SELECT $1, c.id, s.id, $2, '2026-10-05 12:00Z', '2026-10-05 13:00Z', s.preco_centavos
            FROM clientes c, servicos s WHERE c.tenant_id = $1 AND s.tenant_id = $1
            """, tenant, usuario);
        await dono.Exec("INSERT INTO notificacoes (tenant_id, agendamento_id, tipo) SELECT $1, id, 'confirmacao' FROM agendamentos WHERE tenant_id = $1", tenant);
        await dono.Exec("INSERT INTO sessoes (usuario_id, tenant_id, expira_em) VALUES ($1, $2, now() + interval '1 day')", usuario, tenant);
        await tx.CommitAsync();
    }

    internal static Task CriarUsuario(NpgsqlConnection dono, Guid id, string email) =>
        dono.Exec("INSERT INTO usuarios (id, email, senha_hash, nome) VALUES ($1, $2, 'x', 'Teste')", id, email);

    private static async Task<int> Contar(NpgsqlConnection conn, string sql, params object[] args) =>
        await conn.Escalar<int>(sql, args);

    // Roda fn numa transação do app_user com o tenant e espera o SQLSTATE.
    private static async Task ExigirSqlState(NpgsqlConnection app, string codigo, string sql, params object[] args)
    {
        await using var tx = await app.ComTenant(TenantAlfa);
        var erro = await Assert.ThrowsAsync<PostgresException>(() => app.Exec(sql, args));
        Assert.Equal(codigo, erro.SqlState);
    }

    [Fact]
    public async Task IsolamentoPorTenant()
    {
        await using var dono = await Banco.Conectar("MIGRATIONS_DATABASE_URL");
        await using var app = await Banco.Conectar("DATABASE_URL");

        // Armadilha 1 do guia: se a API conecta como dono ou com BYPASSRLS, tudo
        // abaixo passaria por acidente.
        Assert.False(await app.Escalar<bool>("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user"),
            "DATABASE_URL conecta com superusuário ou BYPASSRLS: o RLS não vale para ela");
        Assert.Equal(0, await Contar(app, "SELECT count(*) FROM pg_tables WHERE tableowner = current_user"));

        await ApagarDados(dono);
        try
        {
            await CriarUsuario(dono, UsuarioAlfa, "teste-rls-alfa@teste.invalid");
            await CriarUsuario(dono, UsuarioBeta, "teste-rls-beta@teste.invalid");
            await CriarDadosDoTenant(dono, TenantAlfa, UsuarioAlfa, "teste-rls-alfa");
            await CriarDadosDoTenant(dono, TenantBeta, UsuarioBeta, "teste-rls-beta");

            foreach (var tabela in TabelasComRls)
            {
                await using (await app.ComTenant(TenantAlfa))
                {
                    var todas = await Contar(app, $"SELECT count(*) FROM {tabela}");
                    var doAlfa = await Contar(app, $"SELECT count(*) FROM {tabela} WHERE tenant_id = $1", TenantAlfa);
                    Assert.True(todas > 0 && todas == doAlfa, $"{tabela} sem WHERE: {todas} linhas, do Alfa: {doAlfa}");
                    var doBeta = await Contar(app, $"SELECT count(*) FROM {tabela} WHERE tenant_id = $1", TenantBeta);
                    Assert.True(doBeta == 0, $"{tabela}: WHERE tenant_id = Beta devolveu {doBeta} linhas");
                }
                await using (await app.ComTenant(null))
                {
                    var semTenant = await Contar(app, $"SELECT count(*) FROM {tabela}");
                    Assert.True(semTenant == 0, $"{tabela} sem tenant definido: {semTenant} linhas (devia falhar fechado)");
                }
            }

            // INSERT com tenant de outro falha no WITH CHECK.
            await ExigirSqlState(app, "42501", "INSERT INTO clientes (tenant_id, nome) VALUES ($1, 'invasor')", TenantBeta);
            // UPDATE que muda o tenant falha no WITH CHECK.
            await ExigirSqlState(app, "42501", "UPDATE clientes SET tenant_id = $1", TenantBeta);
            // DELETE em agendamentos é negado.
            await ExigirSqlState(app, "42501", "DELETE FROM agendamentos");

            // O tenant da transação morre com ela, na mesma conexão.
            await using (await app.ComTenant(TenantAlfa))
            {
                Assert.Equal(TenantAlfa.ToString(), await app.Escalar<string>("SELECT app.current_tenant_id()::text"));
            }
            Assert.Null(await app.Escalar<string>("SELECT app.current_tenant_id()::text"));

            // O mesmo pelo EF, que é por onde a API passa: sem WHERE, só o RLS filtra.
            await using var db = new AppDb(AppDb.Opcoes(Banco.ConnectionString("DATABASE_URL")));
            await using (await db.IniciarComTenant(TenantAlfa))
            {
                Assert.Equal([TenantAlfa], await db.Clientes.Select(c => c.TenantId).Distinct().ToListAsync());
                Assert.Equal([TenantAlfa], await db.Agendamentos.Select(a => a.TenantId).Distinct().ToListAsync());
                Assert.Equal([TenantAlfa], await db.Servicos.Select(s => s.TenantId).Distinct().ToListAsync());
                Assert.Equal([TenantAlfa], await db.Disponibilidades.Select(d => d.TenantId).Distinct().ToListAsync());
                Assert.Equal([TenantAlfa], await db.Notificacoes.Select(n => n.TenantId).Distinct().ToListAsync());
            }
            Assert.Empty(await db.Clientes.ToListAsync()); // sem transação com tenant: falha fechada
        }
        finally
        {
            await ApagarDados(dono);
        }
    }
}

// Teste da 0.18: agendamentos referencia clientes, servicos e memberships com
// RESTRICT, e todos cascateiam de tenants. DELETE FROM tenants não pode quebrar
// pela ordem em que o Postgres processa o CASCADE.
//
// Roda como dono das tabelas (MIGRATIONS_DATABASE_URL), como no guia (§009): o app_user não
// tem DELETE em agendamentos e excluir empresa não é operação da API.
public class ExclusaoTenantTests
{
    // UUIDs próprios, fora das faixas do seed (0.16) e do teste de isolamento (0.14).
    private static readonly Guid TenantExcluido = Guid.Parse("f3333333-3333-3333-3333-333333333333");
    private static readonly Guid TenantMantido = Guid.Parse("f4444444-4444-4444-4444-444444444444");
    private static readonly Guid UsuarioExclusao = Guid.Parse("fc000000-0000-0000-0000-000000000001");

    private static async Task ApagarDados(NpgsqlConnection dono)
    {
        await dono.Exec("DELETE FROM tenants WHERE id IN ($1, $2)", TenantExcluido, TenantMantido);
        await dono.Exec("DELETE FROM usuarios WHERE id = $1", UsuarioExclusao);
    }

    [Fact]
    public async Task ExclusaoDeTenant()
    {
        await using var dono = await Banco.Conectar("MIGRATIONS_DATABASE_URL");
        await ApagarDados(dono);
        try
        {
            // O usuário é o mesmo nos dois tenants (criador, administrador e prestador):
            // usuarios é global, então o teste também confere que ele sobrevive.
            await IsolamentoTests.CriarUsuario(dono, UsuarioExclusao, "teste-exclusao@teste.invalid");
            await IsolamentoTests.CriarDadosDoTenant(dono, TenantExcluido, UsuarioExclusao, "teste-exclusao-sai");
            await IsolamentoTests.CriarDadosDoTenant(dono, TenantMantido, UsuarioExclusao, "teste-exclusao-fica");

            // Toda tabela com tenant_id entra sozinha: tabela nova não precisa mexer no teste.
            var tabelas = new List<string>();
            await using (var cmd = new NpgsqlCommand(
                "SELECT table_name FROM information_schema.columns WHERE table_schema = 'public' AND column_name = 'tenant_id'", dono))
            await using (var leitor = await cmd.ExecuteReaderAsync(TestContext.Current.CancellationToken))
            {
                while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
                {
                    tabelas.Add(leitor.GetString(0));
                }
            }
            Assert.Contains("agendamentos", tabelas);
            Assert.Contains("sessoes", tabelas);

            // Conta com o tenant na transação: com FORCE, nem o dono lê sem ele.
            async Task<Dictionary<string, int>> Linhas(Guid tenant)
            {
                var n = new Dictionary<string, int>();
                await using var tx = await dono.ComTenant(tenant);
                foreach (var tabela in tabelas)
                {
                    n[tabela] = await dono.Escalar<int>($"SELECT count(*) FROM {tabela} WHERE tenant_id = $1", tenant);
                }
                return n;
            }

            Assert.All(await Linhas(TenantExcluido), l => Assert.True(l.Value > 0,
                $"{l.Key} sem linha do tenant a excluir: o teste não cobriria a tabela"));
            var antes = await Linhas(TenantMantido);

            await dono.Exec("DELETE FROM tenants WHERE id = $1", TenantExcluido);

            Assert.All(await Linhas(TenantExcluido), l => Assert.True(l.Value == 0,
                $"{l.Key}: sobraram {l.Value} linhas do tenant excluído"));
            Assert.Equal(antes, await Linhas(TenantMantido));
            // usuarios é global: só o membership sai com o tenant.
            Assert.Equal(1, await dono.Escalar<int>("SELECT count(*) FROM usuarios WHERE id = $1", UsuarioExclusao));
        }
        finally
        {
            await ApagarDados(dono);
        }
    }
}

// Teste da 1.2: as consultas, lendo como app_user (DATABASE_URL) com o tenant
// Alfa, não enxergam o Beta. Usa os dados do seed (db/seed.sql, 0.16); a transação
// é desfeita no fim.
public class ConsultasTests
{
    // Período largo: o seed grava datas relativas ao dia em que rodou.
    private static readonly DateTime De = new(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Ate = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AlfaNaoEnxergaBeta()
    {
        await using var db = new AppDb(AppDb.Opcoes(Banco.ConnectionString("DATABASE_URL")));
        await using var tx = await db.IniciarComTenant(Seed.Alfa);

        var clientes = await db.ListarClientes(Seed.Alfa);
        Assert.NotEmpty(clientes); // vazio: o seed (db/seed.sql) rodou?
        Assert.All(clientes, c => Assert.Equal(Seed.Alfa, c.TenantId));

        var agendamentos = await db.ListarAgendamentos(Seed.Alfa, De, Ate, null);
        Assert.NotEmpty(agendamentos);
        Assert.All(agendamentos, a => Assert.Equal(Seed.Alfa, a.Agendamento.TenantId));
        Assert.All(agendamentos, a => Assert.NotEqual("", a.ClienteNome));
        Assert.Equal(agendamentos.OrderBy(a => a.Agendamento.Inicio), agendamentos);
        Assert.NotNull(await db.BuscarAgendamento(Seed.Alfa, Seed.AgendamentoAlfa));
        Assert.NotNull(await db.BuscarCliente(Seed.Alfa, Seed.ClienteAlfa));

        // Registro do Beta por id: não encontrado, pelo WHERE e pelo RLS.
        Assert.Null(await db.BuscarCliente(Seed.Alfa, Seed.ClienteBeta));
        Assert.Null(await db.BuscarAgendamento(Seed.Alfa, Seed.AgendamentoBeta));

        // tenantId do Beta com a transação no Alfa: o RLS barra o que o WHERE deixaria.
        Assert.Empty(await db.ListarClientes(Seed.Beta));
        Assert.Empty(await db.ListarAgendamentos(Seed.Beta, De, Ate, null));
        Assert.Null(await db.BuscarCliente(Seed.Beta, Seed.ClienteBeta));

        // Escrita como app_user passa no WITH CHECK; os DEFAULTs do banco voltam preenchidos.
        var criado = await db.CriarCliente(Seed.Alfa, "Cliente do teste", null);
        Assert.Equal(Seed.Alfa, criado.TenantId);
        Assert.NotEqual(Guid.Empty, criado.Id);
        Assert.InRange(criado.CriadoEm, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(5));
        Assert.InRange(criado.VinculoExpiraEm, DateTime.UtcNow.AddDays(29), DateTime.UtcNow.AddDays(31));
        Assert.True(criado.Notificavel);

        // Cliente removido (exclusão lógica) some da listagem e da busca.
        await db.Database.ExecuteSqlAsync($"UPDATE clientes SET removido_em = now() WHERE id = {criado.Id}");
        Assert.Null(await db.BuscarCliente(Seed.Alfa, criado.Id));
        Assert.DoesNotContain(await db.ListarClientes(Seed.Alfa), c => c.Id == criado.Id);
    }

    // O modelo do EF contra o schema das migrations: coluna com nome ou tipo errado
    // aparece aqui, não na primeira requisição.
    [Fact]
    public async Task ModeloCasaComOSchema()
    {
        await using var db = new AppDb(AppDb.Opcoes(Banco.ConnectionString("DATABASE_URL")));
        await using var tx = await db.IniciarComTenant(Seed.Alfa);

        Assert.NotNull(await db.Tenants.FirstOrDefaultAsync());
        Assert.NotNull(await db.Usuarios.FirstOrDefaultAsync());
        Assert.NotNull(await db.Memberships.FirstOrDefaultAsync());
        Assert.NotNull(await db.Clientes.FirstOrDefaultAsync());
        Assert.NotNull(await db.Servicos.FirstOrDefaultAsync());
        Assert.NotNull(await db.Disponibilidades.FirstOrDefaultAsync());
        Assert.NotNull(await db.Agendamentos.FirstOrDefaultAsync());
        // O seed não grava notificações nem sessões: a consulta confere só os nomes.
        await db.Notificacoes.FirstOrDefaultAsync();
        await db.Sessoes.FirstOrDefaultAsync();
    }

    // contato_publico é dado público da empresa (#112); o CHECK limita o tamanho.
    [Fact]
    public async Task ContatoPublicoAcimaDe120EhRecusado()
    {
        await using var dono = await Banco.Conectar("MIGRATIONS_DATABASE_URL");
        await using var tx = await dono.ComTenant(Seed.Alfa);

        var erro = await Assert.ThrowsAsync<PostgresException>(() =>
            dono.Exec("UPDATE tenants SET contato_publico = repeat('x', 121) WHERE id = $1", Seed.Alfa));
        Assert.Equal("23514", erro.SqlState);
        Assert.Equal("contato_publico_tamanho", erro.ConstraintName);

        // A transação abortou no erro: o caso válido roda em outra, também desfeita.
        await tx.RollbackAsync();
        await using var tx2 = await dono.ComTenant(Seed.Alfa);
        Assert.Equal(1, await dono.Exec("UPDATE tenants SET contato_publico = repeat('x', 120) WHERE id = $1", Seed.Alfa));
    }
}
