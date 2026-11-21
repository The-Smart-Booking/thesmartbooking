namespace Dayspot.Api;

/// <summary>
/// Sessão da Fatia 1, enquanto não há login: TENANT_FIXO e PRESTADOR_FIXO (saem na
/// 2.12, quando o tenant passa a vir da sessão) e SERVICO_FIXO (sai na 6.6). Os
/// handlers recebem pelo contêiner de serviços.
/// </summary>
public sealed record Fixos(Guid TenantId, Guid PrestadorId, Guid ServicoId);

public static class Rotas
{
    // HEAD junto com GET: o corpo não sai, mas a rota responde como no GET.
    private static readonly string[] Leitura = ["GET", "HEAD"];

    /// <summary>
    /// Toda rota fica sob /api (requisitos §12) e todo erro sai no formato de
    /// docs/erros-api.md.
    /// </summary>
    public static void MapApi(this IEndpointRouteBuilder app)
    {
        // Health check do Render e do monitor externo (#172). O ShortCircuit responde já no
        // UseRouting: sem limite, sem tenant e sem transação, então não depende do banco e
        // não reinicia a API enquanto o Neon acorda.
        app.MapMethods("/api/saude", Leitura, () => Results.Ok()).ShortCircuit();

        app.MapPost("/api/login", Login.Entrar);
        app.MapPost("/api/sessao/empresa", Login.EscolherEmpresa);

        app.MapPost("/api/clientes", Clientes.Criar);
        app.MapMethods("/api/clientes", Leitura, Clientes.Listar);
        app.MapMethods("/api/agendamentos", Leitura, Agendamentos.Listar);
        app.MapPost("/api/agendamentos", Agendamentos.Criar);
        app.MapPost("/api/signup", Cadastro.Criar);

        // Sem sessão (#162): ficam fora dos middlewares de autenticação e de tenant.
        var publico = app.MapGroup("/api/publico/{slug}").RequireRateLimiting(Publico.LimiteLeitura);
        publico.MapMethods("", Leitura, Publico.LerEmpresa);
        publico.MapMethods("/servicos", Leitura, Publico.ListarServicos);
        publico.MapMethods("/prestadores", Leitura, Publico.ListarPrestadores);
        publico.MapMethods("/ocupados", Leitura, Publico.ListarOcupados);
        app.MapPost("/api/publico/{slug}/agendamentos", Publico.CriarAgendamento).RequireRateLimiting(Publico.LimiteEnvio);
    }
}
