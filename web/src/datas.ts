// A API fala em UTC (ISO 8601); a tela, no fuso do navegador.

/** Date no formato "AAAA-MM-DD" do <input type="date">, no dia local (toISOString daria o dia em UTC). */
export function diaLocal(d: Date): string {
  const p = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
}

/** "AAAA-MM-DD" + n dias; setDate lida com virada de mês e horário de verão. */
export function somarDias(dia: string, n: number): string {
  const d = new Date(`${dia}T00:00`);
  d.setDate(d.getDate() + n);
  return diaLocal(d);
}

/** Meia-noite local do dia em ISO UTC. "AAAA-MM-DDTHH:MM" sem fuso é lido como hora local. */
export const inicioDoDia = (dia: string) => new Date(`${dia}T00:00`).toISOString();

const formatoData = new Intl.DateTimeFormat("pt-BR", { weekday: "short", day: "numeric", month: "short" });
const formatoHora = new Intl.DateTimeFormat("pt-BR", { hour: "2-digit", minute: "2-digit" });

export const formatarData = (iso: string) => formatoData.format(new Date(iso));
export const formatarHora = (iso: string) => formatoHora.format(new Date(iso));

export const minutosEntre = (inicio: string, fim: string) =>
  Math.round((Date.parse(fim) - Date.parse(inicio)) / 60_000);
