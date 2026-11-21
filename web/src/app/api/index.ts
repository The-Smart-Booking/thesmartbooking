// Funções por recurso. Contrato de JSON definido aqui (colunas das migrations
// 00004 e 00007); os handlers das cards 1.4, 1.5 e 1.6 seguem este formato.
import { HttpClient, HttpParams } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { ApiError, ErroRede } from "./http";

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
  if (e instanceof ApiError && e.codigo === "muitas_tentativas" && e.esperaSegundos) {
    return `Muitas tentativas. Aguarde ${esperaEmTexto(e.esperaSegundos)} e tente de novo.`;
  }
  if (e instanceof ApiError) return mensagens.get(e.codigo) ?? e.mensagem;
  return "Erro inesperado. Tente de novo.";
}

function esperaEmTexto(segundos: number): string {
  if (segundos < 60) return segundos === 1 ? "1 segundo" : `${segundos} segundos`;
  const minutos = Math.ceil(segundos / 60);
  return minutos === 1 ? "1 minuto" : `${minutos} minutos`;
}

/** Valores da coluna agendamentos.status (migration 00007). */
export type StatusAgendamento = "confirmado" | "cancelado" | "concluido";

/** Datas em ISO 8601 UTC com offset, ex.: "2026-10-09T13:00:00Z". */
export interface Agendamento {
  id: string;
  cliente_id: string;
  cliente_nome: string;
  servico_id: string;
  prestador_id: string;
  inicio: string;
  fim: string;
  status: StatusAgendamento;
  valor_centavos: number;
}

export type NovoAgendamento = Pick<Agendamento, "cliente_id" | "inicio" | "fim">;

export interface Cliente {
  id: string;
  nome: string;
  telefone: string | null;
}

/** O que a página pública recebe da empresa; horários são mostrados e enviados neste fuso. */
export interface EmpresaPublica {
  nome: string;
  fuso_horario: string;
  /** Texto livre (telefone, WhatsApp ou e-mail), exibido como texto: é por ele que o cliente cancela. */
  contato: string;
}

/** Serviço da empresa: a duração vem dele, não de quem agenda. */
export interface ServicoPublico {
  id: string;
  nome: string;
  duracao_minutos: number;
  preco_centavos: number;
}

/** Profissional que atende (membership ativo). */
export interface PrestadorPublico {
  id: string;
  nome: string;
}

/** Intervalo ocupado de um profissional, em UTC. */
export interface Ocupado {
  inicio: string;
  fim: string;
}

/** `inicio` em hora local da empresa, sem offset: "2026-11-18T09:30:00". */
export interface NovoAgendamentoPublico {
  servico_id: string;
  prestador_id: string;
  inicio: string;
  nome: string;
  telefone: string;
}

export type AgendamentoPublico = Omit<Agendamento, "cliente_id" | "cliente_nome">;

const rotaPublica = (slug: string) => `/api/publico/${encodeURIComponent(slug)}`;

/** Única porta de entrada para a API. Os erros chegam convertidos pelo errosApiInterceptor. */
@Injectable({ providedIn: "root" })
export class Api {
  private readonly http = inject(HttpClient);

  /** GET /api/agendamentos. Sem de/ate, a API usa o período padrão; sem prestador_id, todos do tenant. */
  listarAgendamentos(de?: string, ate?: string, prestador_id?: string) {
    let params = new HttpParams();
    if (de) params = params.set("de", de);
    if (ate) params = params.set("ate", ate);
    if (prestador_id) params = params.set("prestador_id", prestador_id);
    return this.http.get<Agendamento[]>("/api/agendamentos", { params });
  }

  /** POST /api/agendamentos. 409 conflito_horario quando sobrepõe outro. */
  criarAgendamento(novo: NovoAgendamento) {
    return this.http.post<Omit<Agendamento, "cliente_nome">>("/api/agendamentos", novo);
  }

  /** GET /api/clientes. */
  listarClientes() {
    return this.http.get<Cliente[]>("/api/clientes");
  }

  /** GET /api/publico/{slug}. 404 nao_encontrado quando o slug não existe ou a empresa está sem contato. */
  empresaPublica(slug: string) {
    return this.http.get<EmpresaPublica>(rotaPublica(slug));
  }

  /** GET /api/publico/{slug}/servicos. */
  servicosPublicos(slug: string) {
    return this.http.get<ServicoPublico[]>(`${rotaPublica(slug)}/servicos`);
  }

  /** GET /api/publico/{slug}/prestadores. */
  prestadoresPublicos(slug: string) {
    return this.http.get<PrestadorPublico[]>(`${rotaPublica(slug)}/prestadores`);
  }

  /** GET /api/publico/{slug}/ocupados. `de` e `ate` em RFC 3339, período de até 31 dias. */
  ocupadosPublicos(slug: string, de: string, ate: string, prestador_id: string) {
    const params = new HttpParams().set("de", de).set("ate", ate).set("prestador_id", prestador_id);
    return this.http.get<Ocupado[]>(`${rotaPublica(slug)}/ocupados`, { params });
  }

  /** POST /api/publico/{slug}/agendamentos: cria ou reaproveita o cliente e agenda numa chamada só. */
  criarAgendamentoPublico(slug: string, novo: NovoAgendamentoPublico) {
    return this.http.post<AgendamentoPublico>(`${rotaPublica(slug)}/agendamentos`, novo);
  }
}
