using System.Collections.Concurrent;

namespace Dayspot.Api;

/// <summary>
/// Contador em memória, por chave, de janela fixa: a janela abre na 1ª tentativa e, passado o
/// prazo, a chave recomeça do zero. Conta toda tentativa, inclusive a recusada. O estado é do
/// processo (some no reinício e não é dividido entre instâncias).
/// </summary>
public sealed class LimitePorJanela(int limite, TimeSpan janela, TimeProvider relogio)
{
    private const int MaxChaves = 10_000;
    private static readonly TimeSpan IntervaloVarredura = TimeSpan.FromMinutes(1);

    private sealed class Entrada
    {
        public DateTimeOffset Abertura;
        public int Contagem;
    }

    private readonly ConcurrentDictionary<string, Entrada> _entradas = new();
    private long _ultimaVarredura = 0; // UtcTicks

    /// <summary>Chaves hoje no mapa (para teste).</summary>
    internal int Quantidade => _entradas.Count;

    /// <summary>Conta a tentativa; true se ainda está dentro do limite. Ao recusar, espera é o que falta para a janela fechar.</summary>
    public bool Tentar(string chave, out TimeSpan espera)
    {
        var agora = relogio.GetUtcNow();
        if (_entradas.Count > MaxChaves && (agora.UtcTicks - Volatile.Read(ref _ultimaVarredura)) >= IntervaloVarredura.Ticks)
        {
            Volatile.Write(ref _ultimaVarredura, agora.UtcTicks);
            Varrer(agora);
        }
        var entrada = _entradas.GetOrAdd(chave, _ => new Entrada { Abertura = agora });
        lock (entrada)
        {
            if (agora - entrada.Abertura >= janela)
            {
                entrada.Abertura = agora;
                entrada.Contagem = 0;
            }
            if (entrada.Contagem < limite)
            {
                entrada.Contagem++;
                espera = TimeSpan.Zero;
                return true;
            }
            espera = entrada.Abertura + janela - agora;
            return false;
        }
    }

    // Sem timer: a limpeza pega carona na tentativa que encontra o mapa grande demais, no
    // máximo uma vez por minuto. Se outra thread já tinha pego uma entrada vencida que a
    // varredura removeu, ela conta na entrada órfã: a chave pode aceitar até `limite`
    // tentativas a mais por um instante, e só quando a janela dela já tinha vencido.
    private void Varrer(DateTimeOffset agora)
    {
        foreach (var (chave, entrada) in _entradas)
        {
            bool vencida;
            lock (entrada)
            {
                vencida = agora - entrada.Abertura >= janela;
            }
            if (vencida)
            {
                _entradas.TryRemove(chave, out _);
            }
        }
    }
}
