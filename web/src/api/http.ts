// Única camada que fala com a API. Formato de erro: docs/erros-api.md.

/** Resposta não-2xx da API. Decida pelo `codigo`; `mensagem` pode ser exibida. */
export class ApiError extends Error {
  status: number;
  codigo: string;
  mensagem: string;

  constructor(status: number, codigo: string, mensagem: string) {
    super(mensagem);
    this.name = "ApiError";
    this.status = status;
    this.codigo = codigo;
    this.mensagem = mensagem;
  }
}

/** A requisição nem chegou a ter resposta (API fora do ar, sem conexão). */
export class ErroRede extends Error {
  constructor(causa: unknown) {
    super("Não foi possível conectar ao servidor.", { cause: causa });
    this.name = "ErroRede";
  }
}

async function requisitar<T>(metodo: string, caminho: string, corpo?: unknown): Promise<T> {
  let resp: Response;
  try {
    resp = await fetch(caminho, {
      method: metodo,
      headers: corpo === undefined ? undefined : { "Content-Type": "application/json" },
      body: corpo === undefined ? undefined : JSON.stringify(corpo),
    });
  } catch (e) {
    throw new ErroRede(e);
  }

  if (resp.ok) return (await resp.json()) as T;

  const erro = (await resp.json().catch(() => null))?.erro;
  if (typeof erro?.codigo === "string" && typeof erro?.mensagem === "string") {
    throw new ApiError(resp.status, erro.codigo, erro.mensagem);
  }
  // Corpo fora do formato: 502/503/504 é o proxy (Vite em dev) sem alcançar a API;
  // o resto (HTML de erro etc.) vira erro_interno.
  if ([502, 503, 504].includes(resp.status)) throw new ErroRede(resp.status);
  throw new ApiError(resp.status, "erro_interno", "Erro interno. Tente novamente mais tarde.");
}

export const get = <T>(caminho: string) => requisitar<T>("GET", caminho);
export const post = <T>(caminho: string, corpo: unknown) => requisitar<T>("POST", caminho, corpo);
