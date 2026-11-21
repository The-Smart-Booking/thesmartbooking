using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Dayspot.Api.Dados;

namespace Dayspot.Api.Tests;

internal sealed class RelogioFixo(DateTimeOffset agora) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => agora;
}

/// <summary>
/// Empresa própria de cada teste, gravada pelo dono das tabelas e apagada no fim: as
/// rotas públicas fazem COMMIT de verdade.
/// </summary>
internal sealed class EmpresaDeTeste : IAsyncDisposable
{
    public Guid Tenant { get; } = Guid.NewGuid();
    public Guid Prestador { get; } = Guid.NewGuid();
    public Guid Removido { get; } = Guid.NewGuid();
    public Guid Servico { get; } = Guid.NewGuid();
    public Guid Inativo { get; } = Guid.NewGuid();
    public string Slug { get; }
    public NpgsqlConnection Dono { get; }

    private EmpresaDeTeste(NpgsqlConnection dono) => (Dono, Slug) = (dono, $"t-{Guid.NewGuid():N}");

    public static async Task<EmpresaDeTeste> Criar(string fuso = "America/Sao_Paulo", string? contato = "(11) 3333-4444")
    {
        var e = new EmpresaDeTeste(await Banco.Conectar("MIGRATIONS_DATABASE_URL"));
        await e.Dono.Exec("INSERT INTO usuarios (id, email, senha_hash, nome) VALUES ($1, $2, 'x', 'Paula Prestadora')", e.Prestador, $"{e.Prestador}@teste.example");
        await e.Dono.Exec("INSERT INTO usuarios (id, email, senha_hash, nome) VALUES ($1, $2, 'x', 'Rui Removido')", e.Removido, $"{e.Removido}@teste.example");
        await using var tx = await e.Dono.ComTenant(e.Tenant);
        await e.Dono.Exec("INSERT INTO tenants (id, nome, slug, fuso_horario, contato_publico, criado_por) VALUES ($1, 'Empresa Teste', $2, $3, $4, $5)",
            e.Tenant, e.Slug, fuso, (object?)contato ?? DBNull.Value, e.Prestador);
        await e.Dono.Exec("INSERT INTO memberships (usuario_id, tenant_id, papel) VALUES ($1, $2, 'prestador')", e.Prestador, e.Tenant);
        await e.Dono.Exec("INSERT INTO memberships (usuario_id, tenant_id, papel, removido_em) VALUES ($1, $2, 'prestador', now())", e.Removido, e.Tenant);
        await e.Dono.Exec("INSERT INTO servicos (id, tenant_id, nome, duracao_minutos, preco_centavos) VALUES ($1, $2, 'Corte', 60, 5000)", e.Servico, e.Tenant);
        await e.Dono.Exec("INSERT INTO servicos (id, tenant_id, nome, duracao_minutos, preco_centavos, ativo) VALUES ($1, $2, 'Antigo', 30, 100, false)", e.Inativo, e.Tenant);
        await tx.CommitAsync();
        return e;
    }

    public async Task<int> Clientes() =>
        await Dono.Escalar<int>("SELECT count(*) FROM clientes WHERE tenant_id = $1", Tenant);

    public async Task<int> Agendamentos() =>
        await Dono.Escalar<int>("SELECT count(*) FROM agendamentos WHERE tenant_id = $1", Tenant);

    /// <summary>Corpo válido do POST; extras entram crus no fim do objeto.</summary>
    public string Corpo(string inicio, string telefone = "11 91234-5678", string nome = "Ana Nova", string extras = "") =>
        $$"""{"servico_id":"{{Servico}}","prestador_id":"{{Prestador}}","inicio":"{{inicio}}","nome":"{{nome}}","telefone":"{{telefone}}"{{extras}}}""";

    public async ValueTask DisposeAsync()
    {
        await Dono.Exec("DELETE FROM tenants WHERE id = $1", Tenant); // o CASCADE leva o resto
        await Dono.Exec("DELETE FROM usuarios WHERE id IN ($1, $2)", Prestador, Removido);
        await Dono.DisposeAsync();
    }
}

public class PublicoTests
{
    // Segunda-feira, 12:00 UTC. Os horários dos testes são relativos a este relógio.
    private static readonly RelogioFixo Relogio = new(new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero));
    private const string Segunda = "2026-06-08";

    private static Task<Servidor> Api(TimeProvider? relogio = null) =>
        Servidor.ComPublico(Banco.ConnectionString("DATABASE_URL"), relogio ?? Relogio);

    private static Task<HttpResponseMessage> Agendar(Servidor api, EmpresaDeTeste e, string corpo) =>
        api.Chamar("POST", $"/api/publico/{e.Slug}/agendamentos", corpo);

    private static async Task<JsonElement> ExigirCriado(HttpResponseMessage r)
    {
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == HttpStatusCode.Created, $"esperava 201, veio {(int)r.StatusCode} {corpo}");
        return JsonDocument.Parse(corpo).RootElement;
    }

    private static async Task ExigirCampo(HttpResponseMessage r, string campo)
    {
        await r.ExigirErro(HttpStatusCode.BadRequest, Erros.RequisicaoInvalida);
        Assert.Equal(campo, (await r.Json()).GetProperty("erro").GetProperty("detalhes").GetProperty("campo").GetString());
    }

    private static string[] Chaves(JsonElement objeto) => objeto.EnumerateObject().Select(p => p.Name).ToArray();

    [Fact]
    public async Task EmpresaDevolveSoNomeFusoEContato()
    {
        await using var e = await EmpresaDeTeste.Criar("Asia/Tokyo", "  WhatsApp (11) 3333-4444 ");
        await using var api = await Api();
        var r = await api.Chamar("GET", $"/api/publico/{e.Slug}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var corpo = await r.Json();
        Assert.Equal(["nome", "fuso_horario", "contato"], Chaves(corpo));
        Assert.Equal("Empresa Teste", corpo.GetProperty("nome").GetString());
        Assert.Equal("Asia/Tokyo", corpo.GetProperty("fuso_horario").GetString());
        Assert.Equal("WhatsApp (11) 3333-4444", corpo.GetProperty("contato").GetString());
    }

    // Sem contato a página pública não funciona (#170): mesma resposta do slug inexistente.
    [Theory]
    [InlineData(null, "GET", "")]
    [InlineData(null, "POST", "/agendamentos")]
    [InlineData("", "GET", "")]
    [InlineData("   ", "GET", "/servicos")]
    [InlineData("   ", "POST", "/agendamentos")]
    public async Task EmpresaSemContatoEhNaoEncontrada(string? contato, string metodo, string rota)
    {
        await using var e = await EmpresaDeTeste.Criar(contato: contato);
        await using var api = await Api();
        var r = await api.Chamar(metodo, $"/api/publico/{e.Slug}{rota}", metodo == "POST" ? e.Corpo($"{Segunda}T09:00:00") : null);
        await r.ExigirErro(HttpStatusCode.NotFound, Erros.NaoEncontrado);
        Assert.Equal(0, await e.Agendamentos());
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("GET", "/servicos")]
    [InlineData("GET", "/prestadores")]
    [InlineData("GET", "/ocupados")]
    [InlineData("POST", "/agendamentos")]
    public async Task SlugInexistente(string metodo, string rota)
    {
        await using var api = await Api();
        var r = await api.Chamar(metodo, $"/api/publico/nao-existe-{Guid.NewGuid():N}{rota}", metodo == "POST" ? "{}" : null);
        await r.ExigirErro(HttpStatusCode.NotFound, Erros.NaoEncontrado);
    }

    [Fact]
    public async Task ServicosSoAtivosDaEmpresa()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        var r = await api.Chamar("GET", $"/api/publico/{e.Slug}/servicos");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var s = Assert.Single((await r.Json()).EnumerateArray());
        Assert.Equal(["id", "nome", "duracao_minutos", "preco_centavos"], Chaves(s));
        Assert.Equal(e.Servico, s.GetProperty("id").GetGuid());
        Assert.Equal(60, s.GetProperty("duracao_minutos").GetInt32());
        Assert.Equal(5000, s.GetProperty("preco_centavos").GetInt32());
    }

    [Fact]
    public async Task PrestadoresSoAtivosSemEmail()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        var r = await api.Chamar("GET", $"/api/publico/{e.Slug}/prestadores");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.DoesNotContain("@", await r.Content.ReadAsStringAsync());
        var p = Assert.Single((await r.Json()).EnumerateArray());
        Assert.Equal(["id", "nome"], Chaves(p));
        Assert.Equal(e.Prestador, p.GetProperty("id").GetGuid());
        Assert.Equal("Paula Prestadora", p.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task OcupadosDevolveSoInicioEFim()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00")));
        var cancelado = await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T12:00:00")));
        await e.Dono.Exec("UPDATE agendamentos SET status = 'cancelado' WHERE id = $1", cancelado.GetProperty("id").GetGuid());

        // O período começa no meio do atendimento das 10:00 (13:00Z às 14:00Z): cruza, então vem.
        var r = await api.Chamar("GET", $"/api/publico/{e.Slug}/ocupados?de={Segunda}T13:30:00Z&ate={Segunda}T23:00:00Z&prestador_id={e.Prestador}");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var o = Assert.Single((await r.Json()).EnumerateArray());
        Assert.Equal(["inicio", "fim"], Chaves(o));
        Assert.Equal($"{Segunda}T13:00:00Z", o.GetProperty("inicio").GetString());
        Assert.Equal($"{Segunda}T14:00:00Z", o.GetProperty("fim").GetString());

        // Termina exatamente no início do período: não cruza.
        r = await api.Chamar("GET", $"/api/publico/{e.Slug}/ocupados?de={Segunda}T14:00:00Z&ate={Segunda}T23:00:00Z&prestador_id={e.Prestador}");
        Assert.Empty((await r.Json()).EnumerateArray());
    }

    [Theory]
    [InlineData("ate=2026-06-02T00:00:00Z", "de")]
    [InlineData("de=2026-06-01T00:00:00Z", "ate")]
    [InlineData("de=2026-06-02T00:00:00Z&ate=2026-06-01T00:00:00Z", "ate")]
    [InlineData("de=2026-06-01T00:00:00Z&ate=2026-07-02T00:00:01Z", "ate")] // mais de 31 dias
    [InlineData("de=2026-06-01T00:00:00&ate=2026-06-02T00:00:00Z", "de")] // sem fuso
    public async Task OcupadosPeriodoInvalido(string query, string campo)
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirCampo(await api.Chamar("GET", $"/api/publico/{e.Slug}/ocupados?{query}&prestador_id={e.Prestador}"), campo);
    }

    [Fact]
    public async Task OcupadosDeOutroTenantEhNaoEncontrado()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var outra = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        var r = await api.Chamar("GET", $"/api/publico/{e.Slug}/ocupados?de={Segunda}T00:00:00Z&ate={Segunda}T23:00:00Z&prestador_id={outra.Prestador}");
        await r.ExigirErro(HttpStatusCode.NotFound, Erros.NaoEncontrado);
    }

    [Fact]
    public async Task CriaComServicoPrestadorEValorDoServico()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        // fim, valor e tenant_id no corpo não têm efeito.
        var extras = $$""","fim":"{{Segunda}}T18:00:00","valor_centavos":1,"tenant_id":"{{Seed.Beta}}" """;
        var a = await ExigirCriado(await api.Chamar("POST", $"/api/publico/{e.Slug}/agendamentos?tenant_id={Seed.Beta}", e.Corpo($"{Segunda}T10:00:00", extras: extras)));

        Assert.Equal(["id", "servico_id", "prestador_id", "inicio", "fim", "status", "valor_centavos"], Chaves(a));
        Assert.Equal(e.Servico, a.GetProperty("servico_id").GetGuid());
        Assert.Equal(e.Prestador, a.GetProperty("prestador_id").GetGuid());
        Assert.Equal($"{Segunda}T13:00:00Z", a.GetProperty("inicio").GetString());
        Assert.Equal($"{Segunda}T14:00:00Z", a.GetProperty("fim").GetString()); // 60 min do serviço
        Assert.Equal(5000, a.GetProperty("valor_centavos").GetInt32());

        var id = a.GetProperty("id").GetGuid();
        Assert.Equal(5000, await e.Dono.Escalar<int>("SELECT valor_centavos FROM agendamentos WHERE id = $1", id));
        Assert.Equal(e.Tenant, await e.Dono.Escalar<Guid>("SELECT tenant_id FROM agendamentos WHERE id = $1", id));
        Assert.Equal(e.Tenant, await e.Dono.Escalar<Guid>("SELECT c.tenant_id FROM clientes c JOIN agendamentos a ON a.cliente_id = c.id WHERE a.id = $1", id));
    }

    [Fact]
    public async Task TelefoneCadastradoReaproveitaOClienteMaisAntigo()
    {
        await using var e = await EmpresaDeTeste.Criar();
        var antigo = Guid.NewGuid();
        var novo = Guid.NewGuid();
        var removido = Guid.NewGuid();
        await using (var tx = await e.Dono.ComTenant(e.Tenant))
        {
            await e.Dono.Exec("INSERT INTO clientes (id, tenant_id, nome, telefone, removido_em, criado_em) VALUES ($1, $2, 'Removida', '11912345678', now(), now() - interval '3 days')", removido, e.Tenant);
            await e.Dono.Exec("INSERT INTO clientes (id, tenant_id, nome, telefone, criado_em) VALUES ($1, $2, 'Maria Antiga', '(11) 91234-5678', now() - interval '2 days')", antigo, e.Tenant);
            await e.Dono.Exec("INSERT INTO clientes (id, tenant_id, nome, telefone, criado_em) VALUES ($1, $2, 'Maria Nova', '11 912345678', now() - interval '1 day')", novo, e.Tenant);
            await tx.CommitAsync();
        }
        await using var api = await Api();

        var a = await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00", telefone: "11 91234-5678", nome: "Outro Nome")));

        Assert.Equal(["id", "servico_id", "prestador_id", "inicio", "fim", "status", "valor_centavos"], Chaves(a));
        Assert.DoesNotContain("Maria", a.GetRawText());
        Assert.Equal(antigo, await e.Dono.Escalar<Guid>("SELECT cliente_id FROM agendamentos WHERE id = $1", a.GetProperty("id").GetGuid()));
        Assert.Equal("Maria Antiga", await e.Dono.Escalar<string>("SELECT nome FROM clientes WHERE id = $1", antigo));
        Assert.Equal(3, await e.Clientes());
    }

    [Fact]
    public async Task ClienteRemovidoNaoEhReaproveitado()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using (var tx = await e.Dono.ComTenant(e.Tenant))
        {
            await e.Dono.Exec("INSERT INTO clientes (tenant_id, nome, telefone, removido_em) VALUES ($1, 'Removida', '11912345678', now())", e.Tenant);
            await tx.CommitAsync();
        }
        await using var api = await Api();
        await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00")));
        Assert.Equal(2, await e.Clientes());
    }

    [Fact]
    public async Task SimultaneasComMesmoTelefoneNovoCriamUmClienteETresAgendamentos()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        // Horários diferentes: só o cliente e o limite de 3 por telefone podem colidir.
        var respostas = await Task.WhenAll(Enumerable.Range(8, 8).Select(h => Agendar(api, e, e.Corpo($"{Segunda}T{h:00}:00:00"))));
        Assert.Equal(3, respostas.Count(r => r.StatusCode == HttpStatusCode.Created));
        foreach (var r in respostas.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await ExigirCampo(r, "telefone");
        }
        Assert.Equal(1, await e.Clientes());
        Assert.Equal(3, await e.Agendamentos());
    }

    [Theory]
    [InlineData("912345678")] // 9 dígitos
    [InlineData("119123456789")] // 12 sem o 55
    [InlineData("55119123456789")] // 14 com o 55
    [InlineData("00000000000")]
    [InlineData("(11) 1111-1111")]
    [InlineData("+55 55555555555")]
    public async Task TelefoneInvalido(string telefone)
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirCampo(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00", telefone: telefone)), "telefone");
        Assert.Equal(0, await e.Clientes());
    }

    [Fact]
    public async Task FormatosDoMesmoTelefoneAchamOCadastroAntigo()
    {
        await using var e = await EmpresaDeTeste.Criar();
        var antigo = Guid.NewGuid();
        await using (var tx = await e.Dono.ComTenant(e.Tenant))
        {
            await e.Dono.Exec("INSERT INTO clientes (id, tenant_id, nome, telefone) VALUES ($1, $2, 'Maria', '+55 (11) 91234-5678')", antigo, e.Tenant);
            await tx.CommitAsync();
        }
        await using var api = await Api();
        string[] formatos = ["+55 11 91234-5678", "5511912345678", "11912345678"];
        for (var i = 0; i < formatos.Length; i++)
        {
            var a = await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T1{i}:00:00", telefone: formatos[i])));
            Assert.Equal(antigo, await e.Dono.Escalar<Guid>("SELECT cliente_id FROM agendamentos WHERE id = $1", a.GetProperty("id").GetGuid()));
        }
        Assert.Equal(1, await e.Clientes());
    }

    [Fact]
    public async Task DddCinquentaECincoNaoPerdeODdd()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using (var tx = await e.Dono.ComTenant(e.Tenant))
        {
            // Mesmo número sem o DDD: se o 55 caísse, seria este o achado.
            await e.Dono.Exec("INSERT INTO clientes (tenant_id, nome, telefone) VALUES ($1, 'Sem DDD', '91234-5678')", e.Tenant);
            await tx.CommitAsync();
        }
        await using var api = await Api();
        await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00", telefone: "(55) 91234-5678")));
        await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T11:00:00", telefone: "(55) 3222-1234")));
        await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T12:00:00", telefone: "+55 55 3222-1234")));
        Assert.Equal(3, await e.Clientes()); // o antigo, 55912345678 e 5532221234
    }

    [Fact]
    public async Task QuartoAgendamentoFuturoDoTelefoneEhRecusado()
    {
        await using var e = await EmpresaDeTeste.Criar();
        var ana = Guid.NewGuid();
        var mesma = Guid.NewGuid();
        await using (var tx = await e.Dono.ComTenant(e.Tenant))
        {
            // Dois cadastros com o mesmo telefone normalizado somam no limite.
            await e.Dono.Exec("INSERT INTO clientes (id, tenant_id, nome, telefone) VALUES ($1, $2, 'Ana', '+55 11 91234-5678')", ana, e.Tenant);
            await e.Dono.Exec("INSERT INTO clientes (id, tenant_id, nome, telefone) VALUES ($1, $2, 'Ana B', '11912345678')", mesma, e.Tenant);
            const string insert = """
                INSERT INTO agendamentos (tenant_id, cliente_id, servico_id, prestador_id, inicio, fim, status, valor_centavos)
                VALUES ($1, $2, $3, $4, $5, $5 + interval '1 hour', $6, 0)
                """;
            await e.Dono.Exec(insert, e.Tenant, ana, e.Servico, e.Prestador, new DateTime(2026, 6, 9, 13, 0, 0, DateTimeKind.Utc), "confirmado");
            await e.Dono.Exec(insert, e.Tenant, mesma, e.Servico, e.Prestador, new DateTime(2026, 6, 9, 14, 0, 0, DateTimeKind.Utc), "confirmado");
            // Não contam: cancelado e confirmado que já passou (o relógio está em 01/06 12:00Z).
            await e.Dono.Exec(insert, e.Tenant, ana, e.Servico, e.Prestador, new DateTime(2026, 6, 9, 15, 0, 0, DateTimeKind.Utc), "cancelado");
            await e.Dono.Exec(insert, e.Tenant, ana, e.Servico, e.Prestador, new DateTime(2026, 5, 29, 13, 0, 0, DateTimeKind.Utc), "confirmado");
            await tx.CommitAsync();
        }
        await using var api = await Api();

        await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00")));
        await ExigirCampo(await Agendar(api, e, e.Corpo($"{Segunda}T11:00:00", telefone: "5511912345678", nome: "Outra")), "telefone");
        Assert.Equal(2, await e.Clientes());
        Assert.Equal(5, await e.Agendamentos());
    }

    [Fact]
    public async Task ConflitoNaoDeixaClienteNovo()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00")));
        Assert.Equal(1, await e.Clientes());

        var r = await Agendar(api, e, e.Corpo($"{Segunda}T10:30:00", telefone: "11 95555-0000", nome: "Bia"));
        await r.ExigirErro(HttpStatusCode.Conflict, Erros.ConflitoHorario);
        Assert.Equal(1, await e.Clientes());
        Assert.Equal(1, await e.Agendamentos());
    }

    [Fact]
    public async Task SimultaneasNoMesmoHorarioUmaSoEntra()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        // Oito, abaixo do limite de envios: com duas, o deadlock só aparecia de vez em quando.
        var respostas = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            Agendar(api, e, e.Corpo($"{Segunda}T10:00:00", telefone: $"11 9000-000{i}"))));
        await ExigirCriado(Assert.Single(respostas, r => r.StatusCode != HttpStatusCode.Conflict));
        foreach (var r in respostas.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            await r.ExigirErro(HttpStatusCode.Conflict, Erros.ConflitoHorario);
        }
        Assert.Equal(1, await e.Clientes());
        Assert.Equal(1, await e.Agendamentos());
    }

    // Determinístico: sem o lock por prestador, o segundo INSERT grava a linha e espera a
    // transação do primeiro, não o advisory lock, e é assim que o deadlock começa.
    [Fact]
    public async Task SegundoAgendamentoDoPrestadorEsperaNaFilaAntesDeGravar()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var e = await EmpresaDeTeste.Criar();
        var cliente = Guid.NewGuid();
        await using (var tx = await e.Dono.ComTenant(e.Tenant))
        {
            await e.Dono.Exec("INSERT INTO clientes (id, tenant_id, nome) VALUES ($1, $2, 'Ana')", cliente, e.Tenant);
            await tx.CommitAsync(ct);
        }
        var novo = new NovoAgendamento(cliente, e.Servico, e.Prestador, new DateTime(2026, 6, 8, 13, 0, 0, DateTimeKind.Utc), null);
        var url = Banco.ConnectionString("DATABASE_URL");
        await using var db1 = new AppDb(AppDb.Opcoes(url));
        await using var db2 = new AppDb(AppDb.Opcoes(url));
        await using var tx1 = await db1.IniciarComTenant(e.Tenant, ct);
        await using var tx2 = await db2.IniciarComTenant(e.Tenant, ct);
        Assert.NotNull(await db1.CriarAgendamento(e.Tenant, novo, ct));

        var pid = ((NpgsqlConnection)db2.Database.GetDbConnection()).ProcessID;
        var segundo = db2.CriarAgendamento(e.Tenant, novo, ct);
        var naFila = false;
        for (var i = 0; i < 100 && !naFila; i++)
        {
            await Task.Delay(50, ct);
            naFila = await e.Dono.Escalar<int>("SELECT count(*) FROM pg_locks WHERE pid = $1 AND locktype = 'advisory' AND NOT granted", pid) == 1;
        }
        Assert.True(naFila, "o segundo INSERT não esperou o advisory lock do prestador");

        await tx1.CommitAsync(ct);
        var erro = await Assert.ThrowsAsync<PostgresException>(() => segundo);
        Assert.Equal(PostgresErrorCodes.ExclusionViolation, erro.SqlState);
        Assert.Equal(1, await e.Agendamentos());
    }

    [Theory]
    [InlineData("2026-06-07T10:00:00")] // domingo
    [InlineData("2026-06-08T07:30:00")] // antes das 08:00
    [InlineData("2026-06-08T18:30:00")] // termina 19:30
    [InlineData("2026-06-08T19:00:00")] // começa no fechamento
    [InlineData("2026-06-08T23:30:00")] // termina no dia seguinte
    public async Task ForaDaJanela(string inicio)
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirCampo(await Agendar(api, e, e.Corpo(inicio)), "inicio");
        Assert.Equal(0, await e.Clientes());
    }

    [Theory]
    [InlineData("2026-06-08T08:00:00")] // abre
    [InlineData("2026-06-08T18:00:00")] // termina às 19:00 em ponto
    [InlineData("2026-06-13T10:00")] // sábado, sem segundos
    [InlineData("2026-06-08T09:30:00")] // e meia
    public async Task DentroDaJanela(string inicio)
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirCriado(await Agendar(api, e, e.Corpo(inicio)));
    }

    [Fact]
    public async Task JanelaValeNoFusoDaEmpresa()
    {
        await using var e = await EmpresaDeTeste.Criar("Asia/Tokyo");
        await using var api = await Api();
        // 10:00 em Tóquio (+09:00) = 01:00Z. Lido como UTC ou São Paulo, daria outro instante.
        var a = await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00")));
        Assert.Equal($"{Segunda}T01:00:00Z", a.GetProperty("inicio").GetString());
        Assert.Equal($"{Segunda}T02:00:00Z", a.GetProperty("fim").GetString());
    }

    [Theory]
    [InlineData("2026-06-08T09:15:00")]
    [InlineData("2026-06-08T09:00:30")]
    public async Task ForaDaGradeDeMeiaHora(string inicio)
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirCampo(await Agendar(api, e, e.Corpo(inicio)), "inicio");
        Assert.Equal(0, await e.Clientes());
    }

    [Fact]
    public async Task AntecedenciaDeSessentaDiasNoFusoDaEmpresa()
    {
        await using var e = await EmpresaDeTeste.Criar("Asia/Tokyo");
        // 03/06 20:00Z já é 04/06 em Tóquio: o 60º dia é 03/08 (pela data UTC seria 02/08).
        await using var api = await Api(new RelogioFixo(new DateTimeOffset(2026, 6, 3, 20, 0, 0, TimeSpan.Zero)));
        await ExigirCriado(await Agendar(api, e, e.Corpo("2026-08-03T10:00:00")));
        await ExigirCampo(await Agendar(api, e, e.Corpo("2026-08-04T10:00:00", telefone: "11 95555-0000")), "inicio");
    }

    [Fact]
    public async Task PassadoEhComparadoNoFusoDaEmpresa()
    {
        await using var toquio = await EmpresaDeTeste.Criar("Asia/Tokyo");
        await using var saoPaulo = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        // Agora são 12:00Z. 15:00 em Tóquio = 06:00Z, já passou, embora 15:00 UTC ainda não tenha chegado.
        await ExigirCampo(await Agendar(api, toquio, toquio.Corpo("2026-06-01T15:00:00")), "inicio");
        Assert.Equal(0, await toquio.Clientes());
        // 10:00 em São Paulo = 13:00Z, ainda vem, embora 10:00 UTC já tenha passado.
        await ExigirCriado(await Agendar(api, saoPaulo, saoPaulo.Corpo("2026-06-01T10:00:00")));
    }

    [Theory]
    [InlineData("America/New_York", "2027-03-14T02:30:00")] // o relógio pula das 02:00 às 03:00
    [InlineData("America/New_York", "2027-11-07T01:30:00")] // 01:30 acontece duas vezes
    public async Task HorarioInexistenteOuAmbiguo(string fuso, string inicio)
    {
        await using var e = await EmpresaDeTeste.Criar(fuso);
        await using var api = await Api();
        await ExigirCampo(await Agendar(api, e, e.Corpo(inicio)), "inicio");
    }

    [Theory]
    [InlineData("2026-06-08T10:00:00Z")]
    [InlineData("2026-06-08T10:00:00-03:00")]
    [InlineData("08/06/2026 10:00")]
    [InlineData("")]
    public async Task InicioComFusoOuMalformado(string inicio)
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirCampo(await Agendar(api, e, e.Corpo(inicio)), "inicio");
    }

    [Theory]
    [InlineData("nome", """ "nome":"  " """)]
    [InlineData("telefone", """ "telefone":"sem digitos" """)]
    [InlineData("servico_id", """ "servico_id":"1" """)]
    [InlineData("prestador_id", """ "prestador_id":"1" """)]
    public async Task CampoInvalido(string campo, string substituto)
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        var valido = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(e.Corpo($"{Segunda}T10:00:00"))!;
        var novo = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("{" + substituto + "}")!;
        var corpo = JsonSerializer.Serialize(valido.Concat(novo).GroupBy(p => p.Key).ToDictionary(g => g.Key, g => g.Last().Value));
        await ExigirCampo(await Agendar(api, e, corpo), campo);
        Assert.Equal(0, await e.Clientes());
    }

    [Fact]
    public async Task ServicoOuPrestadorDeOutroTenantEhNaoEncontrado()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var outra = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        string[] corpos =
        [
            e.Corpo($"{Segunda}T10:00:00").Replace(e.Servico.ToString(), outra.Servico.ToString()),
            e.Corpo($"{Segunda}T10:00:00").Replace(e.Prestador.ToString(), outra.Prestador.ToString()),
            e.Corpo($"{Segunda}T10:00:00").Replace(e.Servico.ToString(), e.Inativo.ToString()),
            e.Corpo($"{Segunda}T10:00:00").Replace(e.Prestador.ToString(), e.Removido.ToString()),
        ];
        foreach (var corpo in corpos)
        {
            await (await Agendar(api, e, corpo)).ExigirErro(HttpStatusCode.NotFound, Erros.NaoEncontrado);
        }
        Assert.Equal(0, await e.Clientes()); // o 404 desfaz o cliente novo
        Assert.Equal(0, await outra.Clientes());
    }

    [Fact]
    public async Task MesmoTelefoneEmDuasEmpresasGeraClientesSeparados()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var outra = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        // Três futuros do telefone na outra empresa não contam no limite desta.
        for (var h = 10; h < 13; h++)
        {
            await ExigirCriado(await Agendar(api, outra, outra.Corpo($"{Segunda}T{h}:00:00")));
        }
        await ExigirCriado(await Agendar(api, e, e.Corpo($"{Segunda}T10:00:00")));
        Assert.Equal(1, await e.Clientes());
        Assert.Equal(1, await outra.Clientes());
    }

    [Fact]
    public async Task TenantNaQueryNaoMudaALeitura()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        var r = await api.Chamar("GET", $"/api/publico/{e.Slug}/servicos?tenant_id={Seed.Alfa}");
        Assert.Equal(e.Servico, Assert.Single((await r.Json()).EnumerateArray()).GetProperty("id").GetGuid());
    }
}

// Sem banco: o limite responde antes de qualquer consulta, e as requisições que
// passam dele terminam em 500 por causa do banco fora do ar.
public class LimitePublicoTests
{
    private static Task<Servidor> Api() => Servidor.ComPublico(Banco.ForaDoAr);

    private static Task<HttpResponseMessage> Enviar(Servidor api, string metodo, string rota, string? ip = null, string? encaminhado = null)
    {
        var req = new HttpRequestMessage(new HttpMethod(metodo), rota);
        if (ip is not null)
        {
            req.Headers.Add("CF-Connecting-IP", ip);
        }
        if (encaminhado is not null)
        {
            req.Headers.Add("X-Forwarded-For", encaminhado);
        }
        return api.Http.SendAsync(req);
    }

    private static async Task ExigirLimite(HttpResponseMessage r)
    {
        await r.ExigirErro(HttpStatusCode.TooManyRequests, Erros.MuitasTentativas);
        var segundos = int.Parse(Assert.Single(r.Headers.GetValues("Retry-After")));
        Assert.InRange(segundos, 1, 60);
    }

    [Theory]
    [InlineData("GET", "/api/publico/alfa/servicos", 60)]
    [InlineData("POST", "/api/publico/alfa/agendamentos", 10)]
    public async Task AcimaDoLimite(string metodo, string rota, int limite)
    {
        await using var api = await Api();
        var respostas = await Task.WhenAll(Enumerable.Range(0, limite).Select(_ => Enviar(api, metodo, rota, "203.0.113.1")));
        Assert.DoesNotContain(respostas, r => r.StatusCode == HttpStatusCode.TooManyRequests);
        await ExigirLimite(await Enviar(api, metodo, rota, "203.0.113.1"));
    }

    [Fact]
    public async Task LeituraEEnvioTemLimitesSeparados()
    {
        await using var api = await Api();
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Enviar(api, "POST", "/api/publico/alfa/agendamentos", "203.0.113.1")));
        await ExigirLimite(await Enviar(api, "POST", "/api/publico/alfa/agendamentos", "203.0.113.1"));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Enviar(api, "GET", "/api/publico/alfa", "203.0.113.1")).StatusCode);
    }

    [Fact]
    public async Task XForwardedForForjadoNaoEscapaDoLimite()
    {
        await using var api = await Api();
        const string rota = "/api/publico/alfa/agendamentos";
        await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Enviar(api, "POST", rota, "203.0.113.1", $"198.51.100.{i}")));
        await ExigirLimite(await Enviar(api, "POST", rota, "203.0.113.1", "198.51.100.200"));
    }

    [Fact]
    public async Task CadaClienteTemOProprioLimite()
    {
        await using var api = await Api();
        const string rota = "/api/publico/alfa/agendamentos";
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Enviar(api, "POST", rota, "203.0.113.1")));
        await ExigirLimite(await Enviar(api, "POST", rota, "203.0.113.1"));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Enviar(api, "POST", rota, "203.0.113.2")).StatusCode);
    }

    [Fact]
    public async Task Ipv6ContaPorBloco64()
    {
        await using var api = await Api();
        const string rota = "/api/publico/alfa/agendamentos";
        await Task.WhenAll(Enumerable.Range(1, 10).Select(i => Enviar(api, "POST", rota, $"2001:db8:1:1::{i:x}")));
        await ExigirLimite(await Enviar(api, "POST", rota, "2001:db8:1:1::ff"));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Enviar(api, "POST", rota, "2001:db8:1:2::1")).StatusCode);
    }

    [Fact]
    public async Task LimiteGeraLogSemDadoPessoal()
    {
        var log = new LogEmMemoria();
        await using var api = await Servidor.ComPublico(Banco.ForaDoAr, log: log);
        const string corpo = """{"nome":"Fulana de Teste","telefone":"(11) 98765-4321"}""";
        HttpRequestMessage Pedido()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/publico/alfa/agendamentos")
            {
                Content = new StringContent(corpo, System.Text.Encoding.UTF8, "application/json"),
            };
            req.Headers.Add("CF-Connecting-IP", "203.0.113.1");
            return req;
        }
        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => api.Http.SendAsync(Pedido())));
        Assert.DoesNotContain(log.Linhas, l => l.StartsWith("429 pela política"));
        await ExigirLimite(await api.Http.SendAsync(Pedido()));

        var linha = Assert.Single(log.Linhas, l => l.StartsWith("429 pela política"));
        Assert.Contains(Publico.LimiteEnvio, linha);
        Assert.Contains("/api/publico/{slug}/agendamentos", linha);
        Assert.All(log.Linhas, l =>
        {
            Assert.DoesNotContain("203.0.113", l);
            Assert.DoesNotContain("98765", l);
            Assert.DoesNotContain("Fulana", l);
        });
    }

    [Fact]
    public async Task SaudeRespondeSemBancoEForaDoLimite()
    {
        await using var api = await Api();
        var respostas = await Task.WhenAll(Enumerable.Range(0, 61).Select(_ => Enviar(api, "GET", "/api/saude", "203.0.113.1")));
        Assert.All(respostas, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Enviar(api, "GET", "/api/publico/alfa", "203.0.113.1")).StatusCode);
    }

    [Fact]
    public async Task RotasAutenticadasFicamForaDoLimite()
    {
        await using var api = await Api();
        var respostas = await Task.WhenAll(Enumerable.Range(0, 61).Select(_ => Enviar(api, "GET", "/api/clientes", "203.0.113.1")));
        Assert.DoesNotContain(respostas, r => r.StatusCode == HttpStatusCode.TooManyRequests);
    }
}
