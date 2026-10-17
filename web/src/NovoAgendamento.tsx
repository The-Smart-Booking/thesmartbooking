import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import { criarAgendamento, listarClientes, type Cliente } from "./api";
import { diaLocal } from "./datas";

// Hora digitada, sem slots: os horários livres entram na Fatia 6.
function NovoAgendamento() {
  const navigate = useNavigate();
  const [clientes, setClientes] = useState<Cliente[]>();
  const [erroClientes, setErroClientes] = useState("");
  const [clienteId, setClienteId] = useState("");
  const [data, setData] = useState(() => diaLocal(new Date()));
  const [hora, setHora] = useState("");
  const [duracao, setDuracao] = useState("30");
  const [erro, setErro] = useState("");

  useEffect(() => {
    listarClientes().then(setClientes, (e: Error) => setErroClientes(e.message));
  }, []);

  async function salvar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); // os campos têm required: o navegador barra o envio incompleto antes daqui
    setErro("");
    // "AAAA-MM-DDTHH:MM" sem fuso é hora local; toISOString manda em UTC.
    const inicio = new Date(`${data}T${hora}`);
    const fim = new Date(inicio.getTime() + Number(duracao) * 60_000);
    try {
      await criarAgendamento({ cliente_id: clienteId, inicio: inicio.toISOString(), fim: fim.toISOString() });
      navigate("/agendamentos");
    } catch (e) {
      setErro((e as Error).message); // 409 inclusive: o formulário continua preenchido
    }
  }

  let conteudo;
  if (erroClientes) conteudo = <p className="erro" role="alert">{erroClientes}</p>;
  else if (!clientes) conteudo = <p role="status">Carregando…</p>;
  else if (clientes.length === 0) conteudo = <p className="secundario">Nenhum cliente cadastrado ainda.</p>;
  else
    conteudo = (
      <form className="cartao formulario" onSubmit={salvar}>
        <div>
          <label htmlFor="cliente">Cliente</label>
          <select id="cliente" required value={clienteId} onChange={(e) => setClienteId(e.target.value)}>
            <option value="">Escolha o cliente</option>
            {clientes.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome}
              </option>
            ))}
          </select>
        </div>
        <div className="linha">
          <div>
            <label htmlFor="data">Data</label>
            <input id="data" type="date" required value={data} onChange={(e) => setData(e.target.value)} />
          </div>
          <div>
            <label htmlFor="hora">Hora de início</label>
            <input id="hora" type="time" required value={hora} onChange={(e) => setHora(e.target.value)} />
          </div>
          <div>
            <label htmlFor="duracao">Duração (min)</label>
            <input
              id="duracao"
              type="number"
              required
              min={5}
              max={1440}
              step={5}
              value={duracao}
              onChange={(e) => setDuracao(e.target.value)}
            />
          </div>
        </div>
        {erro && (
          <p className="erro" role="alert">
            {erro}
          </p>
        )}
        <div>
          <button className="botao" type="submit">
            Salvar
          </button>
        </div>
      </form>
    );

  return (
    <>
      <div className="cabecalho">
        <h1>Novo agendamento</h1>
      </div>
      {conteudo}
    </>
  );
}

export default NovoAgendamento;
