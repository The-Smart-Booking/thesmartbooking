import { ChangeDetectionStrategy, Component, ElementRef, inject, signal } from "@angular/core";
import { Router, RouterLink } from "@angular/router";
import { LucideCircleExclamationPoint, LucideEye, LucideEyeOff } from "@lucide/angular";
import { Api, ApiError, mensagemDeErro } from "../api";
import { PaginaEntrada, erroDeEmail, valorDe } from "../entrada/entrada";

/** Envio que passa disso ganha o aviso de "está demorando". */
const MS_LENTO = 5000;

// Entrar (/entrar, #120, especificacao-fluxo §4.2). Só e-mail e senha, sem escolher empresa:
// no piloto, com uma empresa só, o login leva direto à agenda.
@Component({
  selector: "app-entrar",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PaginaEntrada, RouterLink, LucideCircleExclamationPoint, LucideEye, LucideEyeOff],
  templateUrl: "./entrar.html",
})
export class Entrar {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  readonly email = signal("");
  readonly senha = signal("");
  readonly senhaVisivel = signal(false);
  readonly erroEmail = signal("");
  readonly erroSenha = signal("");
  /** Credencial errada, muitas tentativas, sem conexão, erro interno. */
  readonly erroGeral = signal("");
  readonly enviando = signal(false);
  readonly lento = signal(false);

  protected readonly valorDe = valorDe;

  digitarEmail(valor: string) {
    this.email.set(valor);
    this.erroEmail.set("");
    this.erroGeral.set("");
  }

  digitarSenha(valor: string) {
    this.senha.set(valor);
    this.erroSenha.set("");
    this.erroGeral.set("");
  }

  alternarSenha() {
    this.senhaVisivel.update((v) => !v);
  }

  enviar(evento: Event) {
    evento.preventDefault();
    const erroEmail = erroDeEmail(this.email());
    const erroSenha = this.senha() ? "" : "Escreva a senha.";
    this.erroEmail.set(erroEmail);
    this.erroSenha.set(erroSenha);
    if (erroEmail || erroSenha) {
      this.focar(erroEmail ? "e-email" : "e-senha");
      return;
    }
    if (this.enviando()) return;

    this.enviando.set(true);
    this.erroGeral.set("");
    const relogio = setTimeout(() => this.lento.set(true), MS_LENTO);
    const fim = () => {
      clearTimeout(relogio);
      this.enviando.set(false);
      this.lento.set(false);
    };
    this.api.entrar({ email: this.email().trim(), senha: this.senha() }).subscribe({
      next: () => {
        fim();
        void this.router.navigateByUrl("/agendamentos");
      },
      error: (e: unknown) => {
        fim();
        this.erroGeral.set(mensagemDeErro(e));
        // Credencial errada: a senha some e o foco volta para ela; o e-mail fica. Nos outros
        // erros (sem conexão, muitas tentativas) nada do que foi digitado se perde.
        if (e instanceof ApiError && e.codigo === "credencial_invalida") {
          this.senha.set("");
          this.focar("e-senha");
        }
      },
    });
  }

  private focar(id: string) {
    this.host.nativeElement.querySelector<HTMLElement>(`#${id}`)?.focus();
  }
}
