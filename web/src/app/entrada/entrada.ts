// Peças comuns de Entrar e Cadastro (#120): a página com a peça de marca e a validação local.
// Telas no canvas "Dayspot — Entrar" (quadros 1.x e 2.x); componentes v3 no fim de styles.css.
import { ChangeDetectionStrategy, Component, inject } from "@angular/core";
import { LucideMoon, LucideSun } from "@lucide/angular";
import { TemaService } from "../tema";

/** Coluna do formulário à esquerda, peça de marca (degradê, sem texto) à direita. */
@Component({
  selector: "app-pagina-entrada",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LucideMoon, LucideSun],
  template: `
    <div class="entrada-pagina">
      <div class="entrada-coluna">
        <div class="entrada-topo">
          <svg class="entrada-simbolo" viewBox="4 0 56 64" width="24" height="27" role="img" aria-label="Dayspot">
            <path d="M15 6A6 6 0 0 1 21 12V52A6 6 0 0 1 9 52V12A6 6 0 0 1 15 6Z" />
            <path d="M15 58H30A26 26 0 0 0 54.727 23.966A5.976 5.976 0 0 0 43.361 27.659A15 15 0 0 1 29 47H15Z" />
            <path d="M27.25 12.75A7.25 7.25 0 1 0 41.75 12.75A7.25 7.25 0 1 0 27.25 12.75Z" />
          </svg>
          <!-- Mesmo tema da casca do painel (TemaService): a escolha vale para todas as telas. -->
          <button
            type="button"
            class="ds-botao ds-botao--fantasma ds-botao--icone"
            [attr.aria-label]="rotuloTema()"
            [title]="rotuloTema()"
            (click)="tema.trocar()"
          >
            @if (tema.tema() === "escuro") {
              <svg lucideMoon class="ds-ic" aria-hidden="true"></svg>
            } @else {
              <svg lucideSun class="ds-ic" aria-hidden="true"></svg>
            }
          </button>
        </div>
        <ng-content />
      </div>
      <div class="painel-marca" aria-hidden="true">
        <svg viewBox="4 0 56 64" width="154" height="176">
          <path d="M15 6A6 6 0 0 1 21 12V52A6 6 0 0 1 9 52V12A6 6 0 0 1 15 6Z" />
          <path d="M15 58H30A26 26 0 0 0 54.727 23.966A5.976 5.976 0 0 0 43.361 27.659A15 15 0 0 1 29 47H15Z" />
          <path d="M27.25 12.75A7.25 7.25 0 1 0 41.75 12.75A7.25 7.25 0 1 0 27.25 12.75Z" />
        </svg>
      </div>
    </div>
  `,
})
export class PaginaEntrada {
  protected readonly tema = inject(TemaService);

  protected rotuloTema() {
    return this.tema.tema() === "escuro" ? "Tema escuro. Mudar para o claro" : "Tema claro. Mudar para o escuro";
  }
}

/** Texto digitado num `<input>`, para os handlers de (input). */
export function valorDe(evento: Event): string {
  return (evento.target as HTMLInputElement).value;
}

/** Mensagem do campo de e-mail, ou "" se estiver bom. Diz como consertar. */
export function erroDeEmail(email: string): string {
  const e = email.trim();
  if (!e) return "Escreva o e-mail.";
  if (!e.includes("@")) return "Falta o @, como ana@salaoaurora.com.br.";
  if (!/@[^@\s]+\.[^@\s]+$/.test(e)) return "Falta o domínio, como @gmail.com.";
  return "";
}

/** Mesma regra do backend para o endereço da página (especificacao-fluxo §4.1). */
export const SLUG_VALIDO = /^[a-z0-9][a-z0-9-]{1,48}[a-z0-9]$/;

function semAcento(s: string): string {
  return s.normalize("NFD").replace(/[̀-ͯ]/g, "");
}

/** "Salão Aurora" → "salao-aurora": o endereço nasce do nome da empresa. */
export function enderecoDoNome(nome: string): string {
  return semAcento(nome)
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "")
    .slice(0, 50)
    .replace(/-+$/, "");
}

/** Limpa o que a pessoa digita no endereço, sem tirar o hífen do fim enquanto ela escreve. */
export function limparEndereco(digitado: string): string {
  return semAcento(digitado.toLowerCase())
    .replace(/[^a-z0-9-]+/g, "-")
    .replace(/-{2,}/g, "-")
    .replace(/^-+/, "")
    .slice(0, 50);
}
