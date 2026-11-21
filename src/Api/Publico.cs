using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Dayspot.Api.Dados;

namespace Dayspot.Api;

public sealed record EmpresaPublicaJson(string Nome, string FusoHorario, string Contato);

public sealed record ServicoPublicoJson(Guid Id, string Nome, int DuracaoMinutos, int PrecoCentavos);

/// <summary>Sem cliente_id nem dado do cadastro: a resposta não diz se o telefone já existia.</summary>
public sealed record AgendamentoPublicoJson(Guid Id, Guid ServicoId, Guid PrestadorId, DateTime Inicio, DateTime Fim,
    string Status, int ValorCentavos);

// Campo a mais (fim, valor_centavos, tenant_id) é ignorado, em vez do 400 do resto da API:
// nada além destes cinco chega ao banco.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public sealed record NovoAgendamentoPublicoJson(string? ServicoId, string? PrestadorId, string? Inicio, string? Nome, string? Telefone);

/// <summary>
/// Agendamento pelo cliente final, sem conta (#162). O tenant sai do slug da rota,
/// nunca do corpo nem da query string, e entra na transação por set_config como nas
/// demais rotas.
/// </summary>
public static class Publico
{
    public const string LimiteLeitura = "publico-leitura";
    public const string LimiteEnvio = "publico-envio";

    private const int LeiturasPorMinuto = 60;
    private const int EnviosPorMinuto = 10;
    private static readonly TimeSpan JanelaDoLimite = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan PeriodoMaximo = TimeSpan.FromDays(31);
    private static readonly string[] FormatosInicio = ["yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm"];
    private const int DiasDeAntecedencia = 60;
    private const int FuturosPorTelefone = 3;

    // Regra fixa até existir o cadastro de disponibilidade (6.2, 6.3).
    private static readonly TimeSpan Abre = TimeSpan.FromHours(8);
    private static readonly TimeSpan Fecha = TimeSpan.FromHours(19);

    public static IServiceCollection AddPublico(this IServiceCollection servicos)
    {
        servicos.TryAddSingleton(TimeProvider.System);
        servicos.AddIpReal();
        return servicos.AddRateLimiter(o =>
        {
            o.AddPolicy(LimiteLeitura, ctx => PorIp(ctx, LeiturasPorMinuto));
            o.AddPolicy(LimiteEnvio, ctx => PorIp(ctx, EnviosPorMinuto));
            o.OnRejected = (rejeitado, _) =>
            {
                var espera = rejeitado.Lease.TryGetMetadata(MetadataName.RetryAfter, out var t) ? t : JanelaDoLimite;
                var ctx = rejeitado.HttpContext;
                // Só política, método e molde da rota: nada de IP, slug, telefone, nome ou corpo.
                var rota = ctx.GetEndpoint() as RouteEndpoint;
                ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(Publico)).LogWarning(
                    "429 pela política {Politica} em {Metodo} {Rota}",
                    rota?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName, ctx.Request.Method, rota?.RoutePattern.RawText);
                ctx.Response.Headers.RetryAfter = ((int)Math.Ceiling(espera.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                return new ValueTask(Erros.Responder(StatusCodes.Status429TooManyRequests, Erros.MuitasTentativas,
                    "Muitas tentativas. Aguarde um pouco e tente de novo.").ExecuteAsync(ctx));
            };
        });
    }

    /// <summary>
    /// Só nas rotas públicas: IP real, limite e empresa do slug, nessa ordem, antes da
    /// transação. Vem depois do UseRouting e antes do UseTransacaoNasRotas.
    /// </summary>
    public static IApplicationBuilder UsePublico(this IApplicationBuilder app) =>
        app.UseWhen(EhRotaPublica, ramo =>
        {
            ramo.UseForwardedHeaders();
            ramo.UseRateLimiter();
            ramo.Use(ResolverEmpresa);
        });

    /// <summary>Tenant do slug da rota; nulo fora das rotas públicas.</summary>
    public static Guid? TenantDaRota(HttpContext ctx) => (ctx.Items[typeof(EmpresaPublica)] as EmpresaPublica)?.Id;

    private static bool EhRotaPublica(HttpContext ctx) =>
        ctx.GetEndpoint() is RouteEndpoint && ctx.Request.Path.StartsWithSegments("/api/publico");

    private static RateLimitPartition<string> PorIp(HttpContext ctx, int limite) =>
        RateLimitPartition.GetFixedWindowLimiter(IpReal.Chave(ctx), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = limite,
            Window = JanelaDoLimite,
        });

    private static async Task ResolverEmpresa(HttpContext ctx, RequestDelegate proximo)
    {
        EmpresaPublica? empresa;
        try
        {
            var db = ctx.RequestServices.GetRequiredService<AppDb>();
            empresa = await db.BuscarEmpresaPorSlug((string)ctx.GetRouteValue("slug")!, ctx.RequestAborted);
        }
        catch (Exception erro) when (!ctx.RequestAborted.IsCancellationRequested)
        {
            await Erros.Interno(ctx, erro).ExecuteAsync(ctx); // ainda fora do middleware de transação
            return;
        }
        if (empresa is null)
        {
            await Erros.Responder(StatusCodes.Status404NotFound, Erros.NaoEncontrado, "Empresa não encontrada.").ExecuteAsync(ctx);
            return;
        }
        ctx.Items[typeof(EmpresaPublica)] = empresa;
        await proximo(ctx);
    }

    private static EmpresaPublica Empresa(HttpContext ctx) => (EmpresaPublica)ctx.Items[typeof(EmpresaPublica)]!;

    /// <summary>GET /api/publico/{slug}</summary>
    public static IResult LerEmpresa(HttpContext ctx)
    {
        var empresa = Empresa(ctx);
        return Json.Responder(StatusCodes.Status200OK, new EmpresaPublicaJson(empresa.Nome, empresa.FusoHorario, empresa.Contato));
    }

    /// <summary>GET /api/publico/{slug}/servicos: só os ativos.</summary>
    public static async Task<IResult> ListarServicos(HttpContext ctx, AppDb db, CancellationToken ct)
    {
        var servicos = await db.ListarServicosAtivos(Empresa(ctx).Id, ct);
        return Json.Responder(StatusCodes.Status200OK,
            servicos.Select(s => new ServicoPublicoJson(s.Id, s.Nome, s.DuracaoMinutos, s.PrecoCentavos)));
    }

    /// <summary>GET /api/publico/{slug}/prestadores: todo membership ativo.</summary>
    public static async Task<IResult> ListarPrestadores(HttpContext ctx, AppDb db, CancellationToken ct) =>
        Json.Responder(StatusCodes.Status200OK, await db.ListarPrestadoresAtivos(Empresa(ctx).Id, ct));

    /// <summary>
    /// GET /api/publico/{slug}/ocupados?de=&amp;ate=&amp;prestador_id=: só inicio e fim dos
    /// intervalos que cruzam [de, ate), sem cancelados. Datas em RFC 3339.
    /// </summary>
    public static async Task<IResult> ListarOcupados(HttpContext ctx, AppDb db, CancellationToken ct)
    {
        var query = ctx.Request.Query;
        if (!Agendamentos.LerData(query["de"].FirstOrDefault(), "de", out var de, out var erro)
            || !Agendamentos.LerData(query["ate"].FirstOrDefault(), "ate", out var ate, out erro))
        {
            return erro!;
        }
        if (de is null)
        {
            return Erros.CampoInvalido("de", "Informe o início do período.");
        }
        if (ate is null)
        {
            return Erros.CampoInvalido("ate", "Informe o fim do período.");
        }
        if (ate < de)
        {
            return Erros.CampoInvalido("ate", "A data final é anterior à inicial.");
        }
        if (ate - de > PeriodoMaximo)
        {
            return Erros.CampoInvalido("ate", "Período maior que 31 dias.");
        }
        if (!Guid.TryParse(query["prestador_id"].FirstOrDefault(), out var prestadorId))
        {
            return Erros.CampoInvalido("prestador_id", "Informe o profissional.");
        }

        var tenantId = Empresa(ctx).Id;
        if (!await db.PrestadorAtivo(tenantId, prestadorId, ct))
        {
            return Indisponivel();
        }
        return Json.Responder(StatusCodes.Status200OK, await db.ListarOcupados(tenantId, prestadorId, de.Value, ate.Value, ct));
    }

    /// <summary>
    /// POST /api/publico/{slug}/agendamentos. Cliente e agendamento na transação da
    /// requisição: resposta 4xx desfaz os dois. inicio vem em hora local da empresa.
    /// </summary>
    public static async Task<IResult> CriarAgendamento(HttpContext ctx, AppDb db, [FromServices] TimeProvider relogio, CancellationToken ct)
    {
        var entrada = await Json.Ler<NovoAgendamentoPublicoJson>(ctx.Request, ct);
        if (entrada is null)
        {
            return Json.CorpoInvalido();
        }
        if (!Guid.TryParse(entrada.ServicoId, out var servicoId))
        {
            return Erros.CampoInvalido("servico_id", "Informe o serviço.");
        }
        if (!Guid.TryParse(entrada.PrestadorId, out var prestadorId))
        {
            return Erros.CampoInvalido("prestador_id", "Informe o profissional.");
        }
        var nome = (entrada.Nome ?? "").Trim();
        if (nome == "" || Clientes.Caracteres(nome) > Clientes.MaxNome)
        {
            return Erros.CampoInvalido("nome", "Informe o nome (até 200 caracteres).");
        }
        var telefone = (entrada.Telefone ?? "").Trim();
        var digitos = NormalizarTelefone(telefone);
        if (Clientes.Caracteres(telefone) > Clientes.MaxTelefone || digitos.Length is not (10 or 11) || digitos.Distinct().Count() == 1)
        {
            return Erros.CampoInvalido("telefone", "Informe o telefone com DDD (10 ou 11 dígitos).");
        }
        if (!DateTime.TryParseExact(entrada.Inicio, FormatosInicio, CultureInfo.InvariantCulture, DateTimeStyles.None, out var inicio))
        {
            return Erros.CampoInvalido("inicio", "inicio deve ser data e hora locais, sem fuso, ex.: 2026-10-16T09:00:00.");
        }
        if (inicio.Minute % 30 != 0 || inicio.Second != 0)
        {
            return Erros.CampoInvalido("inicio", "Escolha um horário em ponto ou e meia.");
        }

        var empresa = Empresa(ctx);
        var fuso = TimeZoneInfo.FindSystemTimeZoneById(empresa.FusoHorario);
        if (fuso.IsInvalidTime(inicio) || fuso.IsAmbiguousTime(inicio))
        {
            return Erros.CampoInvalido("inicio", "Horário inexistente ou ambíguo no fuso da empresa.");
        }
        var inicioUtc = TimeZoneInfo.ConvertTimeToUtc(inicio, fuso);
        var agora = relogio.GetUtcNow().UtcDateTime;
        if (inicioUtc <= agora)
        {
            return Erros.CampoInvalido("inicio", "Esse horário já passou.");
        }
        if (inicio.Date > TimeZoneInfo.ConvertTimeFromUtc(agora, fuso).Date.AddDays(DiasDeAntecedencia))
        {
            return Erros.CampoInvalido("inicio", $"Agendamento com até {DiasDeAntecedencia} dias de antecedência.");
        }

        var servico = await db.BuscarServicoAtivo(empresa.Id, servicoId, ct);
        if (servico is null)
        {
            return Indisponivel();
        }
        var fim = TimeZoneInfo.ConvertTimeFromUtc(inicioUtc.AddMinutes(servico.DuracaoMinutos), fuso);
        if (inicio.DayOfWeek == DayOfWeek.Sunday || inicio.TimeOfDay < Abre || fim.Date != inicio.Date || fim.TimeOfDay > Fecha)
        {
            return Erros.CampoInvalido("inicio", "Atendimento de segunda a sábado, das 08:00 às 19:00.");
        }

        // Conta depois do lock de telefone que ClienteDoTelefone pega; o 400 desfaz o cliente novo.
        var clienteId = await db.ClienteDoTelefone(empresa.Id, nome, telefone, digitos, ct);
        if (await db.FuturosDoTelefone(empresa.Id, digitos, agora, ct) >= FuturosPorTelefone)
        {
            return Erros.CampoInvalido("telefone", $"Este telefone já tem {FuturosPorTelefone} agendamentos marcados.");
        }
        Agendamento? a;
        try
        {
            a = await db.CriarAgendamento(empresa.Id, new NovoAgendamento(clienteId, servicoId, prestadorId, inicioUtc, null), ct);
        }
        catch (PostgresException e) when (e.SqlState == PostgresErrorCodes.ExclusionViolation)
        {
            return Erros.Responder(StatusCodes.Status409Conflict, Erros.ConflitoHorario, "Já existe um agendamento nesse horário.",
                new() { ["prestador_id"] = prestadorId });
        }
        if (a is null)
        {
            return Indisponivel();
        }
        return Json.Responder(StatusCodes.Status201Created, new AgendamentoPublicoJson(a.Id, a.ServicoId, a.PrestadorId,
            a.Inicio.ToUniversalTime(), a.Fim.ToUniversalTime(), a.Status, a.ValorCentavos));
    }

    /// <summary>Só dígitos; com 12 ou 13 começando em 55, sem o código do país. O SQL de Consultas.ClienteDoTelefone faz o mesmo.</summary>
    internal static string NormalizarTelefone(string telefone)
    {
        var d = string.Concat(telefone.Where(char.IsAsciiDigit));
        return d.Length is 12 or 13 && d.StartsWith("55", StringComparison.Ordinal) ? d[2..] : d;
    }

    private static IResult Indisponivel() =>
        Erros.Responder(StatusCodes.Status404NotFound, Erros.NaoEncontrado, "Profissional ou serviço indisponível.");
}
