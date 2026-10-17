// Estados de tela comuns: carregando, erro e vazio. Nenhuma tela fica em branco.

export function Carregando() {
  return <p role="status">Carregando…</p>;
}

/** Erro com "Tentar de novo" quando há o que repetir (ex.: a busca que falhou). */
export function MensagemErro({ mensagem, onTentarDeNovo }: { mensagem: string; onTentarDeNovo?: () => void }) {
  return (
    <div className="erro" role="alert">
      <span>{mensagem}</span>
      {onTentarDeNovo && (
        <button type="button" className="botao-texto" onClick={onTentarDeNovo}>
          Tentar de novo
        </button>
      )}
    </div>
  );
}

export function Vazio({ children }: { children: string }) {
  return <p className="secundario">{children}</p>;
}
