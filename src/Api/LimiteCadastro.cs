using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dayspot.Api;

/// <summary>Limite de 5 cadastros por hora por IP (#112). Singleton: o contador é do processo.</summary>
public sealed class LimiteCadastro(TimeProvider relogio)
{
    private readonly LimitePorJanela _limite = new(5, TimeSpan.FromHours(1), relogio);

    public bool Tentar(string chave, out TimeSpan espera) => _limite.Tentar(chave, out espera);
}

public static class CadastroServicos
{
    public static IServiceCollection AddCadastro(this IServiceCollection servicos)
    {
        servicos.TryAddSingleton(TimeProvider.System);
        servicos.AddIpReal();
        servicos.AddSingleton<LimiteCadastro>();
        return servicos;
    }

    /// <summary>
    /// IP real só no POST /api/signup, depois do UseRouting e antes do UseTransacaoNasRotas.
    /// O limite em si é checado no handler, antes de ler o corpo.
    /// </summary>
    public static IApplicationBuilder UseCadastro(this IApplicationBuilder app) =>
        app.UseWhen(EhRotaDoCadastro, ramo => ramo.UseForwardedHeaders());

    // Pelo endpoint, não pelo caminho: o roteamento também casa "/api/signup/".
    private static bool EhRotaDoCadastro(HttpContext ctx) =>
        HttpMethods.IsPost(ctx.Request.Method)
        && ctx.GetEndpoint() is RouteEndpoint { RoutePattern.RawText: "/api/signup" };
}
