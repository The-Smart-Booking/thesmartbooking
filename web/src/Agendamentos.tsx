import { useEffect, useState } from "react";
import { Link } from "react-router";
import { listarAgendamentos, type Agendamento } from "./api";
import { diaLocal, formatarData, formatarHora, inicioDoDia, minutosEntre, somarDias } from "./datas";

// Resultado da última busca, marcado com o período que o gerou: enquanto ele não
// bate com o período atual, a tela está carregando (sem setState síncrono no efeito).
type Resultado = { periodo: string } & ({ agendamentos: Agendamento[] } | { erro: string });

function Agendamentos() {
  // Mesmo padrão da API (7 dias a partir de hoje), mas com "hoje" no fuso local.
  const [de, setDe] = useState(() => diaLocal(new Date()));
  const [ate, setAte] = useState(() => somarDias(diaLocal(new Date()), 6));
  const [resultado, setResultado] = useState<Resultado>();

  const periodo = `${de}/${ate}`;
  const invalido = !de || !ate ? "Escolha as duas datas." : ate < de ? "A data final é anterior à inicial." : "";

  useEffect(() => {
    if (invalido) return;
    let atual = true; // descarta a resposta de um período que já mudou
    // "Até" inclui o dia inteiro: a API usa [de, ate), então vai a meia-noite seguinte.
    listarAgendamentos(inicioDoDia(de), inicioDoDia(somarDias(ate, 1))).then(
      (agendamentos) => atual && setResultado({ periodo, agendamentos }),
      (e: Error) => atual && setResultado({ periodo, erro: e.message }),
    );
    return () => {
      atual = false;
    };
  }, [de, ate, periodo, invalido]);

  let conteudo;
  if (invalido) conteudo = <p className="erro">{invalido}</p>;
  else if (resultado?.periodo !== periodo) conteudo = <p role="status">Carregando…</p>;
  else if ("erro" in resultado) conteudo = <p className="erro" role="alert">{resultado.erro}</p>;
  else if (resultado.agendamentos.length === 0)
    conteudo = <p className="secundario">Nenhum agendamento nesse período.</p>;
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
