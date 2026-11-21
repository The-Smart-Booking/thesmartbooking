namespace Dayspot.Api.Tests;

public class LimitePorJanelaTests
{
    private static readonly TimeSpan Hora = TimeSpan.FromHours(1);

    private static (LimitePorJanela Limite, RelogioAjustavel Relogio) Criar()
    {
        var relogio = new RelogioAjustavel(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        return (new LimitePorJanela(5, Hora, relogio), relogio);
    }

    [Fact]
    public void CincoPassamOSextoEhRecusado()
    {
        var (limite, _) = Criar();
        for (var i = 0; i < 5; i++)
        {
            Assert.True(limite.Tentar("a", out _));
        }
        Assert.False(limite.Tentar("a", out var espera));
        Assert.Equal(Hora, espera);
    }

    [Fact]
    public void EsperaEhOQueFaltaDaJanela()
    {
        var (limite, relogio) = Criar();
        Assert.True(limite.Tentar("a", out _));
        relogio.Agora += TimeSpan.FromMinutes(40);
        for (var i = 0; i < 4; i++)
        {
            Assert.True(limite.Tentar("a", out _));
        }
        Assert.False(limite.Tentar("a", out var espera));
        Assert.Equal(TimeSpan.FromMinutes(20), espera);
    }

    [Fact]
    public void PassadaAJanelaVoltaAAceitar()
    {
        var (limite, relogio) = Criar();
        for (var i = 0; i < 6; i++)
        {
            limite.Tentar("a", out _);
        }
        relogio.Agora += Hora;
        Assert.True(limite.Tentar("a", out _));
    }

    [Fact]
    public void ChavesSeparadas()
    {
        var (limite, _) = Criar();
        for (var i = 0; i < 6; i++)
        {
            limite.Tentar("a", out _);
        }
        Assert.False(limite.Tentar("a", out _));
        Assert.True(limite.Tentar("b", out _));
    }

    [Fact]
    public async Task ConcorrenciaNaoPassaDoLimite()
    {
        var (limite, _) = Criar();
        var aceitas = 0;
        await Task.WhenAll(Enumerable.Range(0, 50).Select(i => Task.Run(() =>
        {
            if (limite.Tentar("a", out _))
            {
                Interlocked.Increment(ref aceitas);
            }
        })));
        Assert.Equal(5, aceitas);
    }

    private static void Encher(LimitePorJanela limite, string prefixo, int quantas)
    {
        for (var i = 0; i < quantas; i++)
        {
            limite.Tentar(prefixo + i, out _);
        }
    }

    [Fact]
    public void VarreVencidasEPreservaAsVivas()
    {
        var (limite, relogio) = Criar();
        Encher(limite, "velha", 10_001);
        relogio.Agora += TimeSpan.FromMinutes(30);
        for (var i = 0; i < 5; i++)
        {
            limite.Tentar("viva", out _); // abre a janela dela aqui e chega ao limite
        }
        relogio.Agora += TimeSpan.FromMinutes(45); // as velhas venceram, a viva ainda não
        Assert.Equal(10_002, limite.Quantidade);

        Assert.True(limite.Tentar("nova", out _)); // mapa acima do teto: varre
        Assert.Equal(2, limite.Quantidade); // viva + nova
        Assert.False(limite.Tentar("viva", out _)); // a viva manteve a contagem
    }

    [Fact]
    public void VarreduraNoMaximoUmaVezPorMinuto()
    {
        var relogio = new RelogioAjustavel(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        var limite = new LimitePorJanela(5, TimeSpan.FromSeconds(10), relogio);
        Encher(limite, "a", 10_001);
        relogio.Agora += TimeSpan.FromSeconds(30);
        limite.Tentar("x", out _); // 1ª varredura
        Assert.Equal(1, limite.Quantidade);

        Encher(limite, "b", 10_001);
        relogio.Agora += TimeSpan.FromSeconds(20); // as "b" já venceram, mas faz só 20 s da varredura
        limite.Tentar("y", out _);
        Assert.Equal(10_003, limite.Quantidade);

        relogio.Agora += TimeSpan.FromSeconds(40); // 60 s: varre de novo
        limite.Tentar("z", out _);
        Assert.Equal(1, limite.Quantidade);
    }
}
