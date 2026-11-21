using System.Net;
using System.Text.Json;
using Dayspot.Api.Auth;
using Npgsql;

namespace Dayspot.Api.Tests;

public class CadastroTests
{
    private const string Rota = "/api/signup";

    // Corpo válido: cada caso estraga um campo só.
    private static string Corpo(string campo, object? valor)
    {
        var d = new Dictionary<string, object?>
        {
            ["nome"] = "Ana Souza",
            ["email"] = "ana@alfa.example",
            ["senha"] = "12345678",
            ["empresa_nome"] = "Alfa Barbearia",
            ["empresa_slug"] = "alfa-barbearia",
            ["contato"] = "(11) 99999-0000",
        };
        d[campo] = valor;
        return JsonSerializer.Serialize(d.Where(p => p.Value is not null).ToDictionary(p => p.Key, p => p.Value));
    }

    public static TheoryData<string, string> Invalidos()
    {
        var d = new TheoryData<string, string>();
        void Add(string campo, object? valor) => d.Add(campo, Corpo(campo, valor));
        Add("nome", "");
        Add("nome", "   ");
        Add("nome", new string('a', 201));
        Add("email", "");
        Add("email", "semarroba");
        Add("email", "a@");
        Add("email", "@b");
        Add("email", new string('a', 250) + "@b.co"); // 255
        Add("senha", null);
        Add("senha", "1234567");
        Add("senha", "😀😀😀😀😀😀😀"); // 7 caracteres, 14 unidades UTF-16
        Add("senha", new string('x', 1025));
        Add("empresa_nome", "");
        Add("empresa_nome", new string('a', 121));
        Add("empresa_slug", "");
        Add("empresa_slug", "-ab");
        Add("empresa_slug", "ab");
        Add("empresa_slug", "a b");
        Add("empresa_slug", "loja_1");
        Add("empresa_slug", "lojá");
        Add("empresa_slug", new string('a', 51));
        Add("contato", null);
        Add("contato", "   ");
        Add("contato", new string('a', 121));
        return d;
    }

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task CampoInvalido(string campo, string json)
    {
        await using var api = await Servidor.SemBanco();
        var r = await api.Chamar("POST", Rota, json);
        var corpo = await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(r.StatusCode == HttpStatusCode.BadRequest, $"esperava 400, veio {(int)r.StatusCode} {corpo}");
        var erro = JsonDocument.Parse(corpo).RootElement.GetProperty("erro");
        Assert.Equal(Erros.RequisicaoInvalida, erro.GetProperty("codigo").GetString());
        Assert.Equal(campo, erro.GetProperty("detalhes").GetProperty("campo").GetString());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{"nome":"Ana Souza","email":"ana@alfa.example","senha":"12345678","empresa_nome":"Alfa","empresa_slug":"alfa-barbearia","contato":"x","fuso_horario":"UTC"}""")]
    public async Task CorpoMalformadoOuCampoDesconhecido(string json)
    {
        await using var api = await Servidor.SemBanco();
        await (await api.Chamar("POST", Rota, json)).ExigirErro(HttpStatusCode.BadRequest, Erros.RequisicaoInvalida);
    }

    // Com o banco fora do ar, chegar a ele é a prova de que a validação passou.
    [Theory]
    [InlineData("senha", "12345678")]
    [InlineData("senha", 1024)]
    [InlineData("contato", 120)]
    [InlineData("empresa_nome", 120)]
    [InlineData("empresa_slug", " Minha-Loja ")]
    [InlineData("empresa_slug", "abc")]
    [InlineData("empresa_slug", "slug50")]
    [InlineData("email", "email254")]
    [InlineData("senha", "1234567 ")] // sem Trim: o espaço conta
    [InlineData("senha", "çççççççç")] // 8 caracteres Unicode
    public async Task PassaDaValidacao(string campo, object valor)
    {
        var texto = valor is int n ? new string('x', n) : (string)valor;
        texto = texto switch
        {
            "slug50" => new string('a', 50),
            "email254" => new string('a', 249) + "@b.co",
            _ => texto,
        };
        await using var api = await Servidor.SemBanco();
        // O EF embrulha a falha de conexão em InvalidOperationException; o interno é o do Npgsql.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => api.Chamar("POST", Rota, Corpo(campo, texto)));
        Assert.IsType<NpgsqlException>(erro.InnerException);
    }
}

public class CadastroGravacaoTests
{
    private const string Rota = "/api/signup";

    private static Task<Servidor> Api() => Servidor.ComTransacao(Banco.ConnectionString("DATABASE_URL"), app => app.MapApi());

    private static string Unico() => $"c-{Guid.NewGuid():N}";

    private static string Corpo(string email, string slug, string contato = "  (11) 99999-0000 ") =>
        JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["nome"] = "Ana Souza",
            ["email"] = email,
            ["senha"] = "12345678",
            ["empresa_nome"] = "Loja de Teste",
            ["empresa_slug"] = slug,
            ["contato"] = contato,
        });

    private static Task<HttpResponseMessage> Cadastrar(Servidor api, string corpo) =>
        api.Chamar("POST", Rota, corpo, tenant: Seed.Alfa);

    // Apaga pelo dono das tabelas: tenants antes (criado_por é RESTRICT).
    private static async Task Limpar(NpgsqlConnection dono, string[] slugs, string[] emails)
    {
        foreach (var s in slugs)
        {
            await dono.Exec("DELETE FROM tenants WHERE slug = $1", s);
        }
        foreach (var e in emails)
        {
            await dono.Exec("DELETE FROM usuarios WHERE email = $1", e);
        }
    }

    private static async Task<JsonElement> Exigir(HttpResponseMessage r, HttpStatusCode status)
    {
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.True(r.StatusCode == status, $"esperava {(int)status}, veio {(int)r.StatusCode} {corpo}");
        return JsonDocument.Parse(corpo).RootElement;
    }

    private static async Task ExigirConflito(HttpResponseMessage r, string codigo, string campo)
    {
        await r.ExigirErro(HttpStatusCode.Conflict, codigo);
        Assert.Equal(campo, (await r.Json()).GetProperty("erro").GetProperty("detalhes").GetProperty("campo").GetString());
    }

    [Fact]
    public async Task CadastroValidoCriaUsuarioEmpresaEAdministrador()
    {
        var email = $"{Unico()}@teste.example";
        var slug = Unico();
        await using var dono = await Banco.Conectar("MIGRATIONS_DATABASE_URL");
        try
        {
            await using var api = await Api();
            var corpo = await Exigir(await Cadastrar(api, Corpo($" {email.ToUpperInvariant()} ", slug)), HttpStatusCode.Created);
            Assert.Equal(["usuario_id", "tenant_id", "slug", "papel"], corpo.EnumerateObject().Select(p => p.Name).ToArray());
            Assert.Equal("administrador", corpo.GetProperty("papel").GetString());
            Assert.Equal(slug, corpo.GetProperty("slug").GetString());
            var usuarioId = corpo.GetProperty("usuario_id").GetGuid();
            var tenantId = corpo.GetProperty("tenant_id").GetGuid();

            Assert.Equal(email, await dono.Escalar<string>("SELECT email FROM usuarios WHERE id = $1", usuarioId));
            var hash = await dono.Escalar<string>("SELECT senha_hash FROM usuarios WHERE id = $1", usuarioId);
            Assert.StartsWith("$argon2id$", hash);
            Assert.True(Senha.Conferir("12345678", hash!));
            Assert.Equal(usuarioId, await dono.Escalar<Guid>("SELECT criado_por FROM tenants WHERE id = $1", tenantId));
            Assert.Equal("(11) 99999-0000", await dono.Escalar<string>("SELECT contato_publico FROM tenants WHERE id = $1", tenantId));
            Assert.Equal("America/Sao_Paulo", await dono.Escalar<string>("SELECT fuso_horario FROM tenants WHERE id = $1", tenantId));
            Assert.Equal(1, await dono.Escalar<int>(
                "SELECT count(*) FROM memberships WHERE usuario_id = $1 AND tenant_id = $2 AND papel = 'administrador' AND removido_em IS NULL",
                usuarioId, tenantId));
        }
        finally
        {
            await Limpar(dono, [slug], [email]);
        }
    }

    [Fact]
    public async Task EmailJaUsadoEh409()
    {
        var slug = Unico();
        await using var dono = await Banco.Conectar("MIGRATIONS_DATABASE_URL");
        try
        {
            await using var api = await Api();
            await ExigirConflito(await Cadastrar(api, Corpo(" Admin@Alfa.example ", slug)), Erros.EmailEmUso, "email");
            Assert.Equal(0, await dono.Escalar<int>("SELECT count(*) FROM tenants WHERE slug = $1", slug));
        }
        finally
        {
            await Limpar(dono, [slug], []);
        }
    }

    [Fact]
    public async Task SlugJaUsadoEh409NaoDeixaUsuario()
    {
        await using var e = await EmpresaDeTeste.Criar();
        var email = $"{Unico()}@teste.example";
        await using var api = await Api();
        await ExigirConflito(await Cadastrar(api, Corpo(email, e.Slug)), Erros.SlugEmUso, "empresa_slug");
        Assert.Equal(0, await e.Dono.Escalar<int>("SELECT count(*) FROM usuarios WHERE email = $1", email));
    }

    [Fact]
    public async Task EmailESlugEmUsoEhEmailEmUso()
    {
        await using var e = await EmpresaDeTeste.Criar();
        await using var api = await Api();
        await ExigirConflito(await Cadastrar(api, Corpo("admin@alfa.example", e.Slug)), Erros.EmailEmUso, "email");
    }

    [Fact]
    public async Task SlugGravadoMinusculo()
    {
        var email = $"{Unico()}@teste.example";
        var slug = Unico();
        await using var dono = await Banco.Conectar("MIGRATIONS_DATABASE_URL");
        try
        {
            await using var api = await Api();
            var corpo = await Exigir(await Cadastrar(api, Corpo(email, $" {slug.ToUpperInvariant()} ")), HttpStatusCode.Created);
            Assert.Equal(slug, corpo.GetProperty("slug").GetString());
            Assert.Equal(1, await dono.Escalar<int>("SELECT count(*) FROM tenants WHERE slug = $1", slug));
        }
        finally
        {
            await Limpar(dono, [slug], [email]);
        }
    }

    [Fact]
    public async Task SlugComOutraCaixaEhSlugEmUso()
    {
        var slug = Unico();
        var email1 = $"{Unico()}@teste.example";
        var email2 = $"{Unico()}@teste.example";
        await using var dono = await Banco.Conectar("MIGRATIONS_DATABASE_URL");
        try
        {
            await using var api = await Api();
            await Exigir(await Cadastrar(api, Corpo(email1, slug)), HttpStatusCode.Created);
            await ExigirConflito(await Cadastrar(api, Corpo(email2, slug.ToUpperInvariant())), Erros.SlugEmUso, "empresa_slug");
        }
        finally
        {
            await Limpar(dono, [slug], [email1, email2]);
        }
    }

    [Fact]
    public async Task MesmoEmailSimultaneoCriaUmaConta()
    {
        var email = $"{Unico()}@teste.example";
        var slugs = new[] { Unico(), Unico() };
        await using var dono = await Banco.Conectar("MIGRATIONS_DATABASE_URL");
        try
        {
            await using var api = await Api();
            var respostas = await Task.WhenAll(slugs.Select(s => Cadastrar(api, Corpo(email, s))));
            Assert.Equal([HttpStatusCode.Created, HttpStatusCode.Conflict],
                respostas.Select(r => r.StatusCode).Order().ToArray());
            await ExigirConflito(respostas.Single(r => r.StatusCode == HttpStatusCode.Conflict), Erros.EmailEmUso, "email");
            Assert.Equal(1, await dono.Escalar<int>("SELECT count(*) FROM usuarios WHERE email = $1", email));
        }
        finally
        {
            await Limpar(dono, slugs, [email]);
        }
    }
}

public class LimiteCadastroTests
{
    private const string Rota = "/api/signup";

    private static Task<HttpResponseMessage> Enviar(Servidor api, string ip, string? encaminhado = null, string rota = Rota)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, rota)
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        req.Headers.Add("CF-Connecting-IP", ip);
        if (encaminhado is not null)
        {
            req.Headers.Add("X-Forwarded-For", encaminhado);
        }
        return api.Http.SendAsync(req);
    }

    private static RelogioAjustavel Relogio() => new(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));

    // Corpo inválido: não toca no banco, e a contagem vale para toda requisição.
    private static async Task Esgotar(Servidor api, string ip, Func<int, string?>? encaminhado = null)
    {
        for (var i = 0; i < 5; i++)
        {
            var r = await Enviar(api, ip, encaminhado?.Invoke(i));
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
    }

    private static async Task ExigirLimite(HttpResponseMessage r, int retryAfter)
    {
        await r.ExigirErro(HttpStatusCode.TooManyRequests, Erros.MuitasTentativas);
        Assert.Equal(retryAfter.ToString(), Assert.Single(r.Headers.GetValues("Retry-After")));
    }

    [Fact]
    public async Task SextoCadastroDoMesmoIpEh429()
    {
        await using var api = await Servidor.ComCadastro(Relogio());
        await Esgotar(api, "203.0.113.1");
        await ExigirLimite(await Enviar(api, "203.0.113.1"), 3600);
    }

    [Fact]
    public async Task RetryAfterEhOQueFaltaDaHora()
    {
        var relogio = Relogio();
        await using var api = await Servidor.ComCadastro(relogio);
        await Esgotar(api, "203.0.113.1");
        relogio.Agora += TimeSpan.FromMinutes(45);
        await ExigirLimite(await Enviar(api, "203.0.113.1"), 900);
    }

    [Fact]
    public async Task PassadaAHoraVoltaAAceitar()
    {
        var relogio = Relogio();
        await using var api = await Servidor.ComCadastro(relogio);
        await Esgotar(api, "203.0.113.1");
        relogio.Agora += TimeSpan.FromHours(1);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Enviar(api, "203.0.113.1")).StatusCode);
    }

    [Fact]
    public async Task XForwardedForForjadoNaoEscapa()
    {
        await using var api = await Servidor.ComCadastro(Relogio());
        await Esgotar(api, "203.0.113.1", i => $"198.51.100.{i}");
        await ExigirLimite(await Enviar(api, "203.0.113.1", "198.51.100.200"), 3600);
    }

    [Fact]
    public async Task OutroIpTemOProprioLimite()
    {
        await using var api = await Servidor.ComCadastro(Relogio());
        await Esgotar(api, "203.0.113.1");
        await ExigirLimite(await Enviar(api, "203.0.113.1"), 3600);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Enviar(api, "203.0.113.2")).StatusCode);
    }

    // O roteamento casa a barra final; o IP real e o limite valem igual.
    [Fact]
    public async Task BarraFinalTemOMesmoTratamentoDeIp()
    {
        await using var api = await Servidor.ComCadastro(Relogio());
        for (var i = 0; i < 5; i++)
        {
            var r = await Enviar(api, "203.0.113.1", $"198.51.100.{i}", "/api/signup/");
            Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        }
        await ExigirLimite(await Enviar(api, "203.0.113.1", "198.51.100.200", "/api/signup/"), 3600);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Enviar(api, "203.0.113.2", rota: "/api/signup/")).StatusCode);
    }

    [Fact]
    public async Task Ipv6ContaPorBloco64()
    {
        await using var api = await Servidor.ComCadastro(Relogio());
        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await Enviar(api, $"2001:db8:1:1::{i:x}")).StatusCode);
        }
        await ExigirLimite(await Enviar(api, "2001:db8:1:1::ff"), 3600);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await Enviar(api, "2001:db8:1:2::1")).StatusCode);
    }

    [Fact]
    public async Task LimiteDoCadastroNaoAfetaRotasPublicas()
    {
        await using var api = await Servidor.ComPublico(Banco.ConnectionString("DATABASE_URL"));
        await Esgotar(api, "203.0.113.1");
        await ExigirLimite(await Enviar(api, "203.0.113.1"), 3600);
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/publico/alfa");
        req.Headers.Add("CF-Connecting-IP", "203.0.113.1");
        Assert.NotEqual(HttpStatusCode.TooManyRequests, (await api.Http.SendAsync(req)).StatusCode);
    }
}
