// Funções por recurso. Contrato de JSON definido aqui (colunas das migrations
// 00004 e 00007); os handlers das cards 1.4, 1.5 e 1.6 seguem este formato.
import { get, post } from "./http";

export { ApiError, ErroRede } from "./http";

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

/** GET /api/agendamentos. Sem de/ate, a API usa o período padrão. */
export function listarAgendamentos(de?: string, ate?: string) {
  const q = new URLSearchParams();
  if (de) q.set("de", de);
  if (ate) q.set("ate", ate);
  return get<Agendamento[]>(q.size ? `/api/agendamentos?${q}` : "/api/agendamentos");
}

/** POST /api/agendamentos. 409 conflito_horario quando sobrepõe outro. */
export const criarAgendamento = (novo: NovoAgendamento) =>
  post<Omit<Agendamento, "cliente_nome">>("/api/agendamentos", novo);

/** GET /api/clientes. */
export const listarClientes = () => get<Cliente[]>("/api/clientes");
