using System.Globalization;
using System.Text.RegularExpressions;
using Dayspot.Api.Auth;
using Dayspot.Api.Dados;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Dayspot.Api;

public sealed record NovoCadastroJson(string? Nome, string? Email, string? Senha, string? EmpresaNome, string? EmpresaSlug, string? Contato);

public sealed record CadastroJson(Guid UsuarioId, Guid TenantId, string Slug, string Papel);

/// <summary>
/// Cadastro de empresa e do primeiro administrador (#112). Roda dentro da transação
/// por requisição (UseTransacaoNasRotas, com TENANT_FIXO): usuarios, tenants e
/// memberships ficam fora do RLS (guia §009), então o tenant não importa, e um 4xx
/// desfaz a transação.
/// </summary>
public static partial class Cadastro
{
    public const int MaxNome = 200;
    public const int MinSenha = 8;
    public const int MaxEmpresaNome = 120;
    public const int MaxContato = 120;
    public const int MaxEmail = 254;
    public const int MaxSenha = 1024;

    // Slug já em minúsculas: 3 a 50 caracteres, sem hífen nas pontas.
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$")]
    private static partial Regex PadraoSlug();

    public static async Task<IResult> Criar(HttpContext ctx, AppDb db, [FromServices] LimiteCadastro limite, CancellationToken ct)
    {
        // Antes de ler o corpo; conta toda requisição, inclusive a inválida.
        if (!limite.Tentar(IpReal.Chave(ctx), out var espera))
        {
            ctx.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            return Erros.Responder(StatusCodes.Status429TooManyRequests, Erros.MuitasTentativas,
                "Muitas tentativas de cadastro. Aguarde e tente de novo.");
        }

        var entrada = await Json.Ler<NovoCadastroJson>(ctx.Request, ct);
        if (entrada is null)
        {
            return Json.CorpoInvalido();
        }

        var nome = (entrada.Nome ?? "").Trim();
        if (nome == "" || Clientes.Caracteres(nome) > MaxNome)
        {
            return Erros.CampoInvalido("nome", "Informe o nome (até 200 caracteres).");
        }
        var email = Email.Normalizar(entrada.Email ?? "");
        if (!EmailValido(email))
        {
            return Erros.CampoInvalido("email", "Informe um e-mail válido (até 254 caracteres).");
        }
        // A senha não sofre Trim: espaço nas pontas faz parte dela.
        var senha = entrada.Senha ?? "";
        if (Clientes.Caracteres(senha) < MinSenha)
        {
            return Erros.CampoInvalido("senha", "Senha com no mínimo 8 caracteres.");
        }
        if (Clientes.Caracteres(senha) > MaxSenha)
        {
            return Erros.CampoInvalido("senha", "Senha com no máximo 1024 caracteres.");
        }
        var empresaNome = (entrada.EmpresaNome ?? "").Trim();
        if (empresaNome == "" || Clientes.Caracteres(empresaNome) > MaxEmpresaNome)
        {
            return Erros.CampoInvalido("empresa_nome", "Informe o nome da empresa (até 120 caracteres).");
        }
        var slug = (entrada.EmpresaSlug ?? "").Trim().ToLowerInvariant();
        if (!PadraoSlug().IsMatch(slug))
        {
            return Erros.CampoInvalido("empresa_slug",
                "Endereço com 3 a 50 caracteres: letras, números e hífen, sem hífen nas pontas.");
        }
        var contato = (entrada.Contato ?? "").Trim();
        if (contato == "" || Clientes.Caracteres(contato) > MaxContato)
        {
            return Erros.CampoInvalido("contato", "Informe o contato da empresa (até 120 caracteres).");
        }

        // Depois do 23505 a transação está abortada: nada mais roda SQL neste handler.
        try
        {
            var criada = await db.CriarEmpresa(
                new NovaEmpresa(email, Senha.GerarHash(senha), nome, empresaNome, slug, contato), ct);
            return Json.Responder(StatusCodes.Status201Created,
                new CadastroJson(criada.UsuarioId, criada.TenantId, slug, "administrador"));
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation && e.ConstraintName == "usuarios_email_unico")
        {
            return Erros.Responder(StatusCodes.Status409Conflict, Erros.EmailEmUso, "Este e-mail já tem cadastro.",
                new() { ["campo"] = "email" });
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.UniqueViolation && e.ConstraintName == "tenants_slug_key")
        {
            return Erros.Responder(StatusCodes.Status409Conflict, Erros.SlugEmUso, "Este endereço já está em uso.",
                new() { ["campo"] = "empresa_slug" });
        }
    }

    // Exatamente um @, com texto dos dois lados.
    private static bool EmailValido(string email)
    {
        if (email.Length > MaxEmail)
        {
            return false;
        }
        var arroba = email.IndexOf('@');
        return arroba > 0 && arroba < email.Length - 1 && email.IndexOf('@', arroba + 1) < 0;
    }
}
