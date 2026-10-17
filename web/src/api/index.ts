// Funções por recurso. Contrato de JSON definido aqui (colunas das migrations
// 00004 e 00007); os handlers das cards 1.4, 1.5 e 1.6 seguem este formato.
import { ApiError, ErroRede, get, post } from "./http";

export { ApiError, ErroRede };

// Só os códigos em que um texto fixo diz mais que a mensagem da API; nos outros
// (requisicao_invalida, nao_encontrado, desconhecidos) a mensagem da API já diz o campo.
// Map, não objeto: um codigo como "constructor" não pode cair no protótipo.
const mensagens = new Map([
  ["conflito_horario", "Esse horário acabou de ser ocupado. Escolha outro."],
  ["erro_interno", "Algo deu errado do nosso lado. Tente de novo em instantes."],
]);

/** Texto em português para mostrar ao usuário, decidido pelo `codigo` (docs/erros-api.md). */
export function mensagemDeErro(e: unknown): string {
  if (e instanceof ErroRede) return "Não foi possível falar com o servidor. Tente de novo em instantes.";
  if (e instanceof ApiError) return mensagens.get(e.codigo) ?? e.mensagem;
  return "Erro inesperado. Tente de novo.";
}

/** Datas em ISO 8601 UTC com offset, ex.: "2026-10-09T13:00:00Z". */
export type Agendamento = {
  id: string;
  cliente_id: string;
  cliente_nome: string;
  inicio: string;
  fim: string;
};

export type NovoAgendamento = Pick<Agendamento, "cliente_id" | "inicio" | "fim">;

export type Cliente = {
  id: string;
  nome: string;
  telefone: string | null;
};

/** GET /api/agendamentos. Sem de/ate, a API usa o período padrão; sem prestador_id, todos do tenant. */
export function listarAgendamentos(de?: string, ate?: string, prestador_id?: string) {
  const q = new URLSearchParams();
  if (de) q.set("de", de);
  if (ate) q.set("ate", ate);
  if (prestador_id) q.set("prestador_id", prestador_id);
  return get<Agendamento[]>(q.size ? `/api/agendamentos?${q}` : "/api/agendamentos");
}

/** POST /api/agendamentos. 409 conflito_horario quando sobrepõe outro. */
export const criarAgendamento = (novo: NovoAgendamento) =>
  post<Omit<Agendamento, "cliente_nome">>("/api/agendamentos", novo);

/** GET /api/clientes. */
export const listarClientes = () => get<Cliente[]>("/api/clientes");
