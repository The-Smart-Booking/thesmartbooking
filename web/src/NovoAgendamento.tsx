import { useEffect, useState, type FormEvent } from "react";
import { useNavigate } from "react-router";
import { criarAgendamento, listarClientes, mensagemDeErro, type Cliente } from "./api";
import { diaLocal } from "./datas";
import { Carregando, MensagemErro, Vazio } from "./Estados";

// Hora digitada, sem slots: os horários livres entram na Fatia 6.
function NovoAgendamento() {
  const navigate = useNavigate();
  const [clientes, setClientes] = useState<Cliente[]>();
  const [erroClientes, setErroClientes] = useState("");
  const [tentativa, setTentativa] = useState(0);
  const [clienteId, setClienteId] = useState("");
  const [data, setData] = useState(() => diaLocal(new Date()));
  const [hora, setHora] = useState("");
  const [duracao, setDuracao] = useState("30");
  const [erro, setErro] = useState("");
  const [enviando, setEnviando] = useState(false);

  useEffect(() => {
    listarClientes().then(setClientes, (e) => setErroClientes(mensagemDeErro(e)));
  }, [tentativa]);

  async function salvar(e: FormEvent<HTMLFormElement>) {
    e.preventDefault(); // os campos têm required: o navegador barra o envio incompleto antes daqui
    setErro("");
    setEnviando(true); // o botão desabilitado barra o segundo clique: sem POST duplicado
    // "AAAA-MM-DDTHH:MM" sem fuso é hora local; toISOString manda em UTC.
    const inicio = new Date(`${data}T${hora}`);
    const fim = new Date(inicio.getTime() + Number(duracao) * 60_000);
    try {
      await criarAgendamento({ cliente_id: clienteId, inicio: inicio.toISOString(), fim: fim.toISOString() });
      navigate("/agendamentos");
    } catch (e) {
      setErro(mensagemDeErro(e)); // 409 inclusive: o formulário continua preenchido
      setEnviando(false);
    }
  }

  let conteudo;
  if (erroClientes)
    conteudo = (
      <MensagemErro
        mensagem={erroClientes}
        onTentarDeNovo={() => {
          setErroClientes("");
          setTentativa((t) => t + 1);
        }}
      />
    );
  else if (!clientes) conteudo = <Carregando />;
  else if (clientes.length === 0) conteudo = <Vazio>Nenhum cliente cadastrado ainda.</Vazio>;
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
        {erro && <MensagemErro mensagem={erro} />}
        <div>
          <button className="botao" type="submit" disabled={enviando}>
            {enviando ? "Salvando…" : "Salvar"}
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
