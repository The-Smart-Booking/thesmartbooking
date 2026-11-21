using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Dayspot.Api.Dados;

// Os testes dividem o banco, o diretório atual e as variáveis de ambiente.
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

namespace Dayspot.Api.Tests;

/// <summary>IDs do seed (db/seed.sql, 0.16). Alfa = TENANT_FIXO do .env.example.</summary>
public static class Seed
{
    public static readonly Guid Alfa = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid Beta = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ClienteAlfa = Guid.Parse("11111111-0004-0000-0000-000000000001");
    public static readonly Guid ClienteBeta = Guid.Parse("22222222-0004-0000-0000-000000000001");
    public static readonly Guid ServicoAlfa = Guid.Parse("11111111-0005-0000-0000-000000000001"); // SERVICO_FIXO
    public static readonly Guid AgendamentoAlfa = Guid.Parse("11111111-0007-0000-0000-000000000001");
    public static readonly Guid AgendamentoBeta = Guid.Parse("22222222-0007-0000-0000-000000000001");
    public static readonly Guid AdminAlfa = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Prestador = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"); // PRESTADOR_FIXO
}

public static class Banco
{
    /// <summary>Endereço em que ninguém escuta: qualquer acesso ao banco falha.</summary>
    public const string ForaDoAr = "Host=127.0.0.1;Port=1;Username=app_user;Database=x;Timeout=1";

    /// <summary>
    /// Connection string da variável (DATABASE_URL = app_user; MIGRATIONS_DATABASE_URL = dono
    /// das tabelas). Sem ela, o teste é pulado; no CI (CI definida), falha.
    /// </summary>
    public static string ConnectionString(string variavel)
    {
        var url = Environment.GetEnvironmentVariable(variavel);
        if (string.IsNullOrEmpty(url))
        {
            Assert.False(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI")),
                $"{variavel} não definida no CI: teste de banco não pode pular");
            Assert.Skip($"{variavel} não definida: teste de banco pulado");
        }
        return Config.ParaConnectionString(url!);
    }

    public static async Task<NpgsqlConnection> Conectar(string variavel)
    {
        var conn = new NpgsqlConnection(ConnectionString(variavel));
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>Roda o SQL com parâmetros posicionais ($1, $2...).</summary>
    public static async Task<int> Exec(this NpgsqlConnection conn, string sql, params object[] args)
    {
        await using var cmd = Comando(conn, sql, args);
        return await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<T?> Escalar<T>(this NpgsqlConnection conn, string sql, params object[] args)
    {
        await using var cmd = Comando(conn, sql, args);
        var v = await cmd.ExecuteScalarAsync();
        return v is null or DBNull ? default : (T)Convert.ChangeType(v, Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T));
    }

    private static NpgsqlCommand Comando(NpgsqlConnection conn, string sql, object[] args)
    {
        var cmd = new NpgsqlCommand(sql, conn);
        foreach (var a in args)
        {
            cmd.Parameters.Add(new NpgsqlParameter { Value = a });
        }
        return cmd;
    }

    /// <summary>
    /// Abre transação com set_config(..., true), como a API faz; quem chama desfaz no
    /// Dispose: o teste não deixa rastro. tenant nulo = sem tenant definido.
    /// </summary>
    public static async Task<NpgsqlTransaction> ComTenant(this NpgsqlConnection conn, Guid? tenant)
    {
        var tx = await conn.BeginTransactionAsync();
        if (tenant is not null)
        {
            await conn.Exec("SELECT set_config('app.tenant_id', $1, true)", tenant.Value.ToString());
        }
        return tx;
    }
}

/// <summary>A API num servidor de teste, em memória.</summary>
public sealed class Servidor : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly AppDb? _db;
    private readonly IDbContextTransaction? _tx;

    public HttpClient Http { get; }

    /// <summary>O contexto da transação do teste (só no <see cref="ComBanco"/>).</summary>
    public AppDb Db => _db ?? throw new InvalidOperationException("servidor sem banco");

    private Servidor(WebApplication app, AppDb? db = null, IDbContextTransaction? tx = null)
    {
        (_app, _db, _tx) = (app, db, tx);
        Http = app.GetTestClient();
    }

    public static async Task<Servidor> Criar(Action<IServiceCollection> servicos, Action<WebApplication> pipeline,
        AppDb? db = null, IDbContextTransaction? tx = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        servicos(builder.Services);
        var app = builder.Build();
        pipeline(app);
        await app.StartAsync();
        return new Servidor(app, db, tx);
    }

    /// <summary>
    /// As rotas sobre uma transação do app_user com set_config(..., true), como faz o
    /// UseTransacaoNasRotas, desfeita no fim do teste.
    /// </summary>
    public static async Task<Servidor> ComBanco(Guid tenant)
    {
        var db = new AppDb(AppDb.Opcoes(Banco.ConnectionString("DATABASE_URL")));
        var tx = await db.IniciarComTenant(tenant);
        return await Criar(
            s => s.AddSingleton(db).AddSingleton(new Fixos(tenant, Seed.Prestador, Seed.ServicoAlfa)),
            app => app.MapApi(), db, tx);
    }

    /// <summary>
    /// As rotas para testes de validação: o banco está fora do ar, então tocar nele é
    /// falha (a exceção sobe para o teste).
    /// </summary>
    public static Task<Servidor> SemBanco() => SemBanco(null);

    /// <summary>
    /// <see cref="SemBanco()"/> com o relógio do teste, para o limite de cadastros por IP:
    /// IP real e limite antes das rotas.
    /// </summary>
    public static Task<Servidor> ComCadastro(TimeProvider relogio) => SemBanco(relogio);

    private static Task<Servidor> SemBanco(TimeProvider? relogio) => Criar(
        s =>
        {
            if (relogio is not null)
            {
                s.AddSingleton(relogio);
            }
            s.AddSingleton(new AppDb(AppDb.Opcoes(Banco.ForaDoAr)))
                .AddSingleton(new Fixos(Seed.Alfa, Seed.Prestador, Seed.ServicoAlfa))
                .AddCadastro();
        },
        app =>
        {
            app.UseRouting();
            app.UseCadastro();
            app.MapApi();
        });

    /// <summary>A API inteira, com UseTransacaoNasRotas, sobre a connection string dada.</summary>
    public static Task<Servidor> ComTransacao(string connectionString, Action<WebApplication> rotas) => Criar(
        s => s.AddSingleton(AppDb.Opcoes(connectionString)).AddScoped<AppDb>()
            .AddSingleton(new Fixos(Seed.Alfa, Seed.Prestador, Seed.ServicoAlfa))
            .AddCadastro(),
        app =>
        {
            app.UseRouting();
            app.UseCadastro();
            // No teste, cada requisição escolhe o seu tenant.
            app.UseTransacaoNasRotas(ctx => Guid.Parse(ctx.Request.Headers["Tenant"].ToString()));
            rotas(app);
        });

    /// <summary>O pipeline do Program.cs com as rotas públicas, sobre a connection string dada.</summary>
    public static Task<Servidor> ComPublico(string connectionString, TimeProvider? relogio = null, ILoggerProvider? log = null) => Criar(
        s =>
        {
            if (log is not null)
            {
                s.AddSingleton(log);
            }
            s.AddSingleton(AppDb.Opcoes(connectionString)).AddScoped<AppDb>()
                .AddSingleton(new Fixos(Seed.Alfa, Seed.Prestador, Seed.ServicoAlfa));
            if (relogio is not null)
            {
                s.AddSingleton(relogio);
            }
            s.AddPublico().AddCadastro();
        },
        app =>
        {
            app.UseRouting();
            app.UsePublico();
            app.UseCadastro();
            app.UseTransacaoNasRotas(ctx => Publico.TenantDaRota(ctx) ?? Seed.Alfa);
            app.MapApi();
        });

    public async Task<HttpResponseMessage> Chamar(string metodo, string rota, string? corpo = null, Guid? tenant = null)
    {
        var req = new HttpRequestMessage(new HttpMethod(metodo), rota);
        if (corpo is not null)
        {
            req.Content = new StringContent(corpo, Encoding.UTF8, "application/json");
        }
        if (tenant is not null)
        {
            req.Headers.Add("Tenant", tenant.Value.ToString());
        }
        return await Http.SendAsync(req);
    }

    public async ValueTask DisposeAsync()
    {
        Http.Dispose();
        await _app.DisposeAsync();
        if (_tx is not null)
        {
            await _tx.DisposeAsync(); // ROLLBACK
        }
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }
    }
}

/// <summary>Guarda cada linha de log formatada, de qualquer categoria.</summary>
public sealed class LogEmMemoria : ILoggerProvider, ILogger
{
    public System.Collections.Concurrent.ConcurrentQueue<string> Linhas { get; } = new();
    public ILogger CreateLogger(string categoria) => this;
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel nivel) => true;
    public void Log<TState>(LogLevel nivel, EventId id, TState state, Exception? erro, Func<TState, Exception?, string> formatar) =>
        Linhas.Enqueue(formatar(state, erro) + erro);
    public void Dispose() { }
}

public static class Respostas
{
    public static async Task<JsonElement> Json(this HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    /// <summary>Confere status e código no formato de docs/erros-api.md.</summary>
    public static async Task ExigirErro(this HttpResponseMessage r, HttpStatusCode status, string codigo)
    {
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"esperava {(int)status} {codigo}, veio {(int)r.StatusCode} {corpo}");
        var erro = JsonDocument.Parse(corpo).RootElement.GetProperty("erro");
        Assert.Equal(codigo, erro.GetProperty("codigo").GetString());
        Assert.NotEqual("", erro.GetProperty("mensagem").GetString());
    }
}

/// <summary>Relógio que o teste avança à mão.</summary>
public sealed class RelogioAjustavel(DateTimeOffset inicio) : TimeProvider
{
    public DateTimeOffset Agora { get; set; } = inicio;

    public override DateTimeOffset GetUtcNow() => Agora;
}
