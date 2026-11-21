using Microsoft.EntityFrameworkCore;

namespace Dayspot.Api.Dados;

/// <summary>O que vem da requisição. Fim nulo = inicio + duração do serviço.</summary>
public sealed record NovoAgendamento(Guid ClienteId, Guid ServicoId, Guid PrestadorId, DateTime Inicio, DateTime? Fim);

public sealed record AgendamentoComCliente(Agendamento Agendamento, string ClienteNome);

/// <summary>O que o cadastro grava: e-mail já normalizado, senha já em hash, slug já em minúsculas.</summary>
public sealed record NovaEmpresa(string Email, string SenhaHash, string Nome, string EmpresaNome, string Slug, string Contato);

public sealed record EmpresaCriada(Guid UsuarioId, Guid TenantId);

public sealed record EmpresaPublica(Guid Id, string Nome, string FusoHorario, string Contato);

public sealed record EmpresaDoUsuario(Guid TenantId, string Nome, string Slug, string Papel);

public sealed record PrestadorPublico(Guid Id, string Nome);

public sealed record Ocupado(DateTime Inicio, DateTime Fim);

/// <summary>
/// As consultas da API. Toda uma recebe tenantId logo depois do contexto e filtra
/// por ele, mesmo com RLS: é a camada (b) de defesa (requisitos §6.2). Rodam dentro
/// da transação aberta por <see cref="AppDb.IniciarComTenant"/>.
/// </summary>
public static class Consultas
{
    // Cliente removido (exclusão lógica) não aparece.
    public static Task<List<Cliente>> ListarClientes(this AppDb db, Guid tenantId, CancellationToken ct = default) =>
        db.Clientes.Where(c => c.TenantId == tenantId && c.RemovidoEm == null)
            .OrderBy(c => c.Nome)
            .ToListAsync(ct);

    /// <summary>Nulo: inexistente, removido ou de outro tenant (o RLS nem deixa ver).</summary>
    public static Task<Cliente?> BuscarCliente(this AppDb db, Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.Clientes.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id && c.RemovidoEm == null, ct);

    public static async Task<Cliente> CriarCliente(this AppDb db, Guid tenantId, string nome, string? telefone, CancellationToken ct = default)
    {
        var c = new Cliente { TenantId = tenantId, Nome = nome, Telefone = telefone };
        db.Clientes.Add(c);
        await db.SaveChangesAsync(ct);
        db.Entry(c).State = EntityState.Detached; // o contexto não guarda o que gravou
        return c;
    }

    /// <summary>
    /// Agendamentos com inicio em [de, ate), com o nome do cliente, para a tela não
    /// fazer uma chamada por linha (1.6). prestadorId nulo = a empresa toda: quem
    /// monta o filtro é o escopo por prestador (2.7), nunca o handler.
    /// </summary>
    public static Task<List<AgendamentoComCliente>> ListarAgendamentos(this AppDb db, Guid tenantId,
        DateTime de, DateTime ate, Guid? prestadorId, CancellationToken ct = default) =>
        (from a in db.Agendamentos
         join c in db.Clientes
             on new { Id = a.ClienteId, a.TenantId } equals new { c.Id, c.TenantId }
         where a.TenantId == tenantId && a.Inicio >= de && a.Inicio < ate
             && (prestadorId == null || a.PrestadorId == prestadorId)
         orderby a.Inicio
         select new AgendamentoComCliente(a, c.Nome)).ToListAsync(ct);

    /// <summary>Nulo: inexistente ou de outro tenant.</summary>
    public static Task<Agendamento?> BuscarAgendamento(this AppDb db, Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.Agendamentos.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    /// <summary>
    /// Uma instrução só (1.5): valor_centavos copiado do serviço, nunca do payload;
    /// prestador só com membership ativo e serviço só ativo, senão zero linhas (devolve
    /// nulo). Sem fim, inicio + duração do serviço. Sobreposição fica com a constraint
    /// sem_sobreposicao: sai como PostgresException 23P01, porque checar antes no
    /// código abre condição de corrida. A constraint checa depois de gravar a linha:
    /// dois INSERTs simultâneos no mesmo horário esperariam um pelo outro (deadlock,
    /// 40P01). O advisory lock por prestador os põe em fila até o fim da transação, e
    /// vem sempre depois do lock de telefone (<see cref="ClienteDoTelefone"/>).
    /// </summary>
    public static async Task<Agendamento?> CriarAgendamento(this AppDb db, Guid tenantId, NovoAgendamento n, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({tenantId.ToString()} || ':prestador:' || {n.PrestadorId.ToString()}, 0))", ct);
        var criados = await db.Agendamentos.FromSql($"""
            INSERT INTO agendamentos (tenant_id, cliente_id, servico_id, prestador_id, inicio, fim, valor_centavos)
            SELECT s.tenant_id, {n.ClienteId}, s.id, m.usuario_id, {n.Inicio},
                   COALESCE({n.Fim}::timestamptz, {n.Inicio} + s.duracao_minutos * interval '1 minute'),
                   s.preco_centavos
            FROM servicos s
            JOIN memberships m
              ON m.tenant_id = s.tenant_id AND m.usuario_id = {n.PrestadorId} AND m.removido_em IS NULL
            WHERE s.tenant_id = {tenantId} AND s.id = {n.ServicoId} AND s.ativo
            RETURNING *
            """).ToListAsync(ct);
        return criados.SingleOrDefault();
    }

    /// <summary>
    /// Cria usuário, empresa e membership de administrador numa instrução só. Exceção à
    /// regra "toda consulta recebe tenantId": é ela que cria o tenant, e as três
    /// tabelas ficam fora do RLS (guia §009). E-mail ou slug repetido sobe como
    /// PostgresException 23505 (usuarios_email_unico, tenants_slug_key), e a transação
    /// da requisição desfaz o que já tinha entrado.
    /// </summary>
    public static async Task<EmpresaCriada> CriarEmpresa(this AppDb db, NovaEmpresa n, CancellationToken ct = default)
    {
        // O SqlQuery mapeia o resultado pela convenção snake_case (RETURNING usuario_id, tenant_id):
        // alias como AS "UsuarioId" quebra o mapeamento.
        var criadas = await db.Database.SqlQuery<EmpresaCriada>($"""
            WITH u AS (
              INSERT INTO usuarios (email, senha_hash, nome)
              VALUES ({n.Email}, {n.SenhaHash}, {n.Nome})
              RETURNING id
            ), t AS (
              INSERT INTO tenants (nome, slug, contato_publico, criado_por)
              SELECT {n.EmpresaNome}, {n.Slug}, {n.Contato}, u.id FROM u
              RETURNING id, criado_por
            )
            INSERT INTO memberships (usuario_id, tenant_id, papel)
            SELECT criado_por, id, 'administrador' FROM t
            RETURNING usuario_id, tenant_id
            """).ToListAsync(ct);
        return criadas.Single();
    }

    /// <summary>
    /// A única consulta sem tenantId: é ela que descobre o tenant, antes da transação.
    /// tenants fica fora do RLS (guia §009), e só estas colunas saem daqui. Nulo também
    /// sem contato (vazio ou só espaços): a página pública exige contato (#170).
    /// </summary>
    public static async Task<EmpresaPublica?> BuscarEmpresaPorSlug(this AppDb db, string slug, CancellationToken ct = default)
    {
        var t = await db.Tenants.Where(t => t.Slug == slug)
            .Select(t => new { t.Id, t.Nome, t.FusoHorario, t.ContatoPublico })
            .FirstOrDefaultAsync(ct);
        var contato = t?.ContatoPublico?.Trim();
        return string.IsNullOrEmpty(contato) ? null : new EmpresaPublica(t!.Id, t.Nome, t.FusoHorario, contato);
    }

    public static Task<List<Servico>> ListarServicosAtivos(this AppDb db, Guid tenantId, CancellationToken ct = default) =>
        db.Servicos.Where(s => s.TenantId == tenantId && s.Ativo)
            .OrderBy(s => s.Nome)
            .ToListAsync(ct);

    /// <summary>Nulo: inexistente, inativo ou de outro tenant.</summary>
    public static Task<Servico?> BuscarServicoAtivo(this AppDb db, Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.Servicos.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Id == id && s.Ativo, ct);

    /// <summary>memberships e usuarios ficam fora do RLS: o filtro por tenant é este WHERE.</summary>
    public static Task<List<PrestadorPublico>> ListarPrestadoresAtivos(this AppDb db, Guid tenantId, CancellationToken ct = default) =>
        (from m in db.Memberships
         join u in db.Usuarios on m.UsuarioId equals u.Id
         where m.TenantId == tenantId && m.RemovidoEm == null
         orderby u.Nome
         select new PrestadorPublico(u.Id, u.Nome)).ToListAsync(ct);

    public static Task<bool> PrestadorAtivo(this AppDb db, Guid tenantId, Guid id, CancellationToken ct = default) =>
        db.Memberships.AnyAsync(m => m.TenantId == tenantId && m.UsuarioId == id && m.RemovidoEm == null, ct);

    /// <summary>Intervalos do prestador que cruzam [de, ate); cancelado libera o horário.</summary>
    public static Task<List<Ocupado>> ListarOcupados(this AppDb db, Guid tenantId, Guid prestadorId,
        DateTime de, DateTime ate, CancellationToken ct = default) =>
        db.Agendamentos
            .Where(a => a.TenantId == tenantId && a.PrestadorId == prestadorId && a.Status != "cancelado"
                && a.Inicio < ate && a.Fim > de)
            .OrderBy(a => a.Inicio)
            .Select(a => new Ocupado(a.Inicio, a.Fim))
            .ToListAsync(ct);

    /// <summary>
    /// Cliente do tenant com este telefone normalizado (com mais de um, o mais antigo);
    /// sem nenhum, cria. telefone não tem UNIQUE e pode ter duplicados gravados, então
    /// um índice único quebraria na migration: o advisory lock serializa por tenant e
    /// telefone até o fim da transação, sem tocar nos dados.
    /// </summary>
    public static async Task<Guid> ClienteDoTelefone(this AppDb db, Guid tenantId, string nome, string telefone, string digitos,
        CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({tenantId.ToString()} || ':' || {digitos}, 0))", ct);
        // Varre os clientes do tenant; índice em expressão se a base crescer.
        // Gravado só em dígitos e sem o 55 do país quando sobram 10 ou 11, como em
        // Publico.NormalizarTelefone: cadastros antigos com +55 ou máscara não são reescritos.
        var achados = await db.Database.SqlQuery<Guid>($$"""
            SELECT c.id AS "Value" FROM clientes c
            WHERE c.tenant_id = {{tenantId}} AND c.removido_em IS NULL
              AND regexp_replace(regexp_replace(c.telefone, '[^0-9]', '', 'g'), '^55([0-9]{10,11})$', '\1') = {{digitos}}
            ORDER BY c.criado_em, c.id
            LIMIT 1
            """).ToListAsync(ct);
        return achados.Count > 0 ? achados[0] : (await db.CriarCliente(tenantId, nome, telefone, ct)).Id;
    }

    /// <summary>
    /// Agendamentos confirmados depois de agora, somando os clientes ativos com este
    /// telefone normalizado. Chamar com o lock de <see cref="ClienteDoTelefone"/> já pego.
    /// </summary>
    public static Task<int> FuturosDoTelefone(this AppDb db, Guid tenantId, string digitos, DateTime agora,
        CancellationToken ct = default) =>
        db.Database.SqlQuery<int>($$"""
            SELECT count(*)::int AS "Value" FROM agendamentos a
            JOIN clientes c ON c.id = a.cliente_id AND c.tenant_id = a.tenant_id
            WHERE a.tenant_id = {{tenantId}} AND a.status = 'confirmado' AND a.inicio > {{agora}}
              AND c.removido_em IS NULL
              AND regexp_replace(regexp_replace(c.telefone, '[^0-9]', '', 'g'), '^55([0-9]{10,11})$', '\1') = {{digitos}}
            """).SingleAsync(ct);

    // Login e sessão (2.3): usuarios, memberships, tenants e sessoes ficam fora do RLS
    // (guia §009), porque são lidas antes de existir tenant. O filtro é o WHERE manual.

    /// <summary>E-mail já normalizado; o lower() casa com o índice usuarios_email_unico.</summary>
    public static Task<Usuario?> BuscarUsuarioPorEmail(this AppDb db, string email, CancellationToken ct = default) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);

    /// <summary>Empresas em que o usuário tem membership ativo, em ordem de nome.</summary>
    public static Task<List<EmpresaDoUsuario>> ListarEmpresasDoUsuario(this AppDb db, Guid usuarioId, CancellationToken ct = default) =>
        (from m in db.Memberships
         join t in db.Tenants on m.TenantId equals t.Id
         where m.UsuarioId == usuarioId && m.RemovidoEm == null
         orderby t.Nome
         select new EmpresaDoUsuario(t.Id, t.Nome, t.Slug, m.Papel)).ToListAsync(ct);

    /// <summary>
    /// Grava a sessão e devolve o id, que vai no cookie. tenantId nulo = empresa ainda
    /// não escolhida (várias memberships).
    /// </summary>
    public static async Task<Guid> CriarSessao(this AppDb db, Guid usuarioId, Guid? tenantId, TimeSpan duracao, string? userAgent,
        CancellationToken ct = default)
    {
        var ids = await db.Database.SqlQuery<Guid>($"""
            INSERT INTO sessoes (usuario_id, tenant_id, expira_em, user_agent)
            VALUES ({usuarioId}, {tenantId}, now() + {duracao}, {userAgent})
            RETURNING id AS "Value"
            """).ToListAsync(ct);
        return ids.Single();
    }

    /// <summary>Nulo: sessão inexistente, expirada ou revogada.</summary>
    public static Task<Sessao?> BuscarSessaoValida(this AppDb db, Guid id, CancellationToken ct = default) =>
        db.Sessoes.FirstOrDefaultAsync(s => s.Id == id && s.RevogadoEm == null && s.ExpiraEm > DateTime.UtcNow, ct);

    /// <summary>
    /// Grava na sessão a empresa do slug, só se o usuário da sessão tem membership ativo
    /// nela. Zero linhas: slug inexistente ou sem membership ativo, e a sessão não muda.
    /// </summary>
    public static Task<int> GravarEmpresaDaSessao(this AppDb db, Guid sessaoId, string slug, CancellationToken ct = default) =>
        db.Database.ExecuteSqlAsync($"""
            UPDATE sessoes s SET tenant_id = t.id
            FROM tenants t
            JOIN memberships m ON m.tenant_id = t.id AND m.removido_em IS NULL
            WHERE s.id = {sessaoId} AND t.slug = {slug} AND m.usuario_id = s.usuario_id
            """, ct);
}
