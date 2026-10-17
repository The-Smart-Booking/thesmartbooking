import { useEffect, useState } from "react";
import { Link } from "react-router";
import { listarAgendamentos, mensagemDeErro, type Agendamento } from "./api";
import { Carregando, MensagemErro, Vazio } from "./Estados";
import { diaLocal, formatarData, formatarHora, inicioDoDia, minutosEntre, somarDias } from "./datas";

// Resultado da última busca, marcado com a busca que o gerou (período + tentativa):
// enquanto ele não bate com a atual, a tela está carregando (sem setState síncrono no efeito).
type Resultado = { busca: string } & ({ agendamentos: Agendamento[] } | { erro: string });

function Agendamentos() {
  // Mesmo padrão da API (7 dias a partir de hoje), mas com "hoje" no fuso local.
  const [de, setDe] = useState(() => diaLocal(new Date()));
  const [ate, setAte] = useState(() => somarDias(diaLocal(new Date()), 6));
  const [tentativa, setTentativa] = useState(0);
  const [resultado, setResultado] = useState<Resultado>();

  const busca = `${de}/${ate}/${tentativa}`;
  const invalido = !de || !ate ? "Escolha as duas datas." : ate < de ? "A data final é anterior à inicial." : "";

  useEffect(() => {
    if (invalido) return;
    let atual = true; // descarta a resposta de uma busca que já mudou
    // "Até" inclui o dia inteiro: a API usa [de, ate), então vai a meia-noite seguinte.
    listarAgendamentos(inicioDoDia(de), inicioDoDia(somarDias(ate, 1))).then(
      (agendamentos) => atual && setResultado({ busca, agendamentos }),
      (e) => atual && setResultado({ busca, erro: mensagemDeErro(e) }),
    );
    return () => {
      atual = false;
    };
  }, [de, ate, busca, invalido]);

  let conteudo;
  if (invalido) conteudo = <MensagemErro mensagem={invalido} />;
  else if (resultado?.busca !== busca) conteudo = <Carregando />;
  else if ("erro" in resultado)
    conteudo = <MensagemErro mensagem={resultado.erro} onTentarDeNovo={() => setTentativa((t) => t + 1)} />;
  else if (resultado.agendamentos.length === 0) conteudo = <Vazio>Nenhum agendamento nesse período.</Vazio>;
  else
    conteudo = (
      <ul className="cartao lista">
        {resultado.agendamentos.map((a) => (
          <li key={a.id}>
            <time dateTime={a.inicio}>
              <span className="secundario">{formatarData(a.inicio)}</span> {formatarHora(a.inicio)}
            </time>
            <span>{a.cliente_nome}</span>
            <span className="secundario">{minutosEntre(a.inicio, a.fim)} min</span>
          </li>
        ))}
      </ul>
    );

  return (
    <>
      <div className="cabecalho">
        <h1>Agendamentos</h1>
        <Link className="botao" to="/agendamentos/novo">
          Novo agendamento
        </Link>
      </div>
      <div className="filtro">
        <div>
          <label htmlFor="de">De</label>
          <input id="de" type="date" value={de} onChange={(e) => setDe(e.target.value)} />
        </div>
        <div>
          <label htmlFor="ate">Até</label>
          <input id="ate" type="date" value={ate} onChange={(e) => setAte(e.target.value)} />
        </div>
      </div>
      {conteudo}
    </>
  );
}

export default Agendamentos;
