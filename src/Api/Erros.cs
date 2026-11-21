using System.Text.Json.Serialization;

namespace Dayspot.Api;

public sealed record CorpoErro(DetalheErro Erro);

public sealed record DetalheErro(
    string Codigo,
    string Mensagem,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, object>? Detalhes);

/// <summary>Todo erro da API sai por aqui, no formato de docs/erros-api.md.</summary>
public static class Erros
{
    // Códigos de erro estáveis. O frontend decide pelo código, nunca pela mensagem.
    public const string RequisicaoInvalida = "requisicao_invalida"; // 400
    public const string NaoAutenticado = "nao_autenticado"; // 401
    public const string CredencialInvalida = "credencial_invalida"; // 401
    public const string NaoEncontrado = "nao_encontrado"; // 404
    public const string MetodoNaoPermitido = "metodo_nao_permitido"; // 405
    public const string ConflitoHorario = "conflito_horario"; // 409
    public const string EmailEmUso = "email_em_uso"; // 409
    public const string SlugEmUso = "slug_em_uso"; // 409
    public const string MuitasTentativas = "muitas_tentativas"; // 429
    public const string ErroInterno = "erro_interno"; // 500

    /// <summary>Erro no formato padrão. detalhes pode ser nulo.</summary>
    public static IResult Responder(int status, string codigo, string mensagem, Dictionary<string, object>? detalhes = null) =>
        Json.Responder(status, new CorpoErro(new DetalheErro(codigo, mensagem, detalhes)));

    /// <summary>400 apontando o campo, para o formulário marcar.</summary>
    public static IResult CampoInvalido(string campo, string mensagem) =>
        Responder(StatusCodes.Status400BadRequest, RequisicaoInvalida, mensagem, new() { ["campo"] = campo });

    /// <summary>
    /// Loga o erro e responde 500 genérico: mensagem do Postgres (ou qualquer erro
    /// interno) nunca vai para o cliente.
    /// </summary>
    public static IResult Interno(HttpContext ctx, Exception erro)
    {
        ctx.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("Dayspot.Api")
            .LogError(erro, "erro interno: {Metodo} {Rota}", ctx.Request.Method, ctx.Request.Path);
        return Responder(StatusCodes.Status500InternalServerError, ErroInterno, "Erro interno. Tente novamente mais tarde.");
    }
}
