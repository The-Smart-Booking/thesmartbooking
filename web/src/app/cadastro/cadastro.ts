import { ChangeDetectionStrategy, Component, ElementRef, Injector, afterNextRender, inject, signal } from "@angular/core";
import { Router, RouterLink } from "@angular/router";
import { LucideChevronLeft, LucideCircleExclamationPoint, LucideEye, LucideEyeOff } from "@lucide/angular";
import { Api, ApiError, mensagemDeErro } from "../api";
import { PaginaEntrada, SLUG_VALIDO, enderecoDoNome, erroDeEmail, limparEndereco, valorDe } from "../entrada/entrada";

type Campo = "nome" | "email" | "senha" | "empresa" | "endereco" | "contato";
type Erros = Partial<Record<Campo, string>>;

/** Envio que passa disso ganha o aviso de "está demorando". */
const MS_LENTO = 5000;

/** `detalhes.campo` do POST /api/signup → campo da tela. */
const CAMPOS_DA_API = new Map<string, Campo>([
  ["nome", "nome"],
  ["email", "email"],
  ["senha", "senha"],
  ["empresa_nome", "empresa"],
  ["empresa_slug", "endereco"],
  ["contato", "contato"],
]);

export function campoDaTela(campoApi: string): Campo | undefined {
  return CAMPOS_DA_API.get(campoApi);
}

/** Texto fixo onde ele diz mais que a mensagem da API; nos outros, vale a da API. */
const TEXTOS_DE_CONFLITO = new Map([
  ["email_em_uso", "Este e-mail já tem conta."],
  ["slug_em_uso", "Esse endereço já está em uso. Tente outro."],
]);

// Cadastro (/cadastro, #120, especificacao-fluxo §4.1) em duas etapas no mesmo formulário:
// 1. sua conta (nome, e-mail, senha) e 2. sua empresa (nome, endereço da página e contato).
// "Continuar" só confere os campos; nada é gravado antes de "Criar conta e abrir a agenda".
// O contato é obrigatório (decisão de 20/11/2026) e aparece na página pública (#170).
@Component({
  selector: "app-cadastro",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [PaginaEntrada, RouterLink, LucideChevronLeft, LucideCircleExclamationPoint, LucideEye, LucideEyeOff],
  templateUrl: "./cadastro.html",
})
export class Cadastro {
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly injector = inject(Injector);
  private readonly api = inject(Api);
  private readonly router = inject(Router);

  readonly etapa = signal<1 | 2>(1);
  readonly nome = signal("");
  readonly email = signal("");
  readonly senha = signal("");
  readonly senhaVisivel = signal(false);
  readonly empresa = signal("");
  readonly endereco = signal("");
  readonly contato = signal("");
  /** Depois que a pessoa mexe no endereço, ele para de acompanhar o nome da empresa. */
  private enderecoEditado = false;
  readonly erros = signal<Erros>({});
  /** 409 email_em_uso: o erro do e-mail ganha o link para Entrar. */
  readonly emailJaTemConta = signal(false);
  /** Erro que não é de um campo: muitas tentativas, sem conexão, erro interno. */
  readonly erroGeral = signal("");
  readonly enviando = signal(false);
  readonly lento = signal(false);

  protected readonly valorDe = valorDe;

  digitar(campo: "nome" | "email" | "senha", valor: string) {
    this[campo].set(valor);
    this.limparErro(campo);
  }

  digitarEmpresa(valor: string) {
    this.empresa.set(valor);
    this.limparErro("empresa");
    if (!this.enderecoEditado) {
      this.endereco.set(enderecoDoNome(valor));
      this.limparErro("endereco");
    }
  }

  digitarEndereco(evento: Event) {
    const limpo = limparEndereco(valorDe(evento));
    // Reescreve o campo na hora para a pessoa ver o endereço como ele vai ficar.
    (evento.target as HTMLInputElement).value = limpo;
    this.endereco.set(limpo);
    this.enderecoEditado = true;
    this.limparErro("endereco");
  }

  digitarContato(valor: string) {
    this.contato.set(valor);
    this.limparErro("contato");
  }

  alternarSenha() {
    this.senhaVisivel.update((v) => !v);
  }

  continuar(evento: Event) {
    evento.preventDefault();
    const erros: Erros = {};
    if (!this.nome().trim()) erros.nome = "Escreva seu nome.";
    const erroEmail = erroDeEmail(this.email());
    if (erroEmail) erros.email = erroEmail;
    if (!this.senha()) erros.senha = "Escreva uma senha.";
    else if (this.senha().length < 8) erros.senha = "A senha precisa de pelo menos 8 caracteres.";
    this.erros.set(erros);

    const primeiro = (["nome", "email", "senha"] as const).find((c) => erros[c]);
    if (primeiro) {
      this.focar(`k-${primeiro}`);
      return;
    }
    this.irPara(2);
  }

  voltar() {
    this.irPara(1);
  }

  criar(evento: Event) {
    evento.preventDefault();
    const erros: Erros = {};
    if (!this.empresa().trim()) erros.empresa = "Escreva o nome da empresa.";
    else if (this.empresa().trim().length > 120) erros.empresa = "Use até 120 caracteres.";
    if (!this.endereco()) erros.endereco = "Escreva o endereço, como salao-aurora.";
    else if (!SLUG_VALIDO.test(this.endereco())) {
      erros.endereco = "Use de 3 a 50 letras sem acento, números ou hífen.";
    }
    if (!this.contato().trim()) erros.contato = "Escreva um contato, como (11) 98765-4321.";
    else if (this.contato().trim().length > 120) erros.contato = "Use até 120 caracteres.";
    this.erros.set(erros);

    const primeiro = (["empresa", "endereco", "contato"] as const).find((c) => erros[c]);
    if (primeiro) {
      this.focar(`k-${primeiro}`);
      return;
    }
    if (this.enviando()) return;
    this.enviar();
  }

  private enviar() {
    this.enviando.set(true);
    this.erroGeral.set("");
    const relogio = setTimeout(() => this.lento.set(true), MS_LENTO);
    const fim = () => {
      clearTimeout(relogio);
      this.enviando.set(false);
      this.lento.set(false);
    };
    this.api
      .cadastrar({
        nome: this.nome().trim(),
        email: this.email().trim(),
        senha: this.senha(),
        empresa_nome: this.empresa().trim(),
        empresa_slug: this.endereco(),
        contato: this.contato().trim(),
      })
      .subscribe({
        next: () => {
          fim();
          void this.router.navigateByUrl("/agendamentos");
        },
        error: (e: unknown) => {
          fim();
          this.mostrarErro(e);
        },
      });
  }

  /** Erro com campo vai para o campo (voltando à etapa 1, se for dela); o resto vira aviso geral. */
  private mostrarErro(e: unknown) {
    const campo = e instanceof ApiError && e.campo ? campoDaTela(e.campo) : undefined;
    if (!(e instanceof ApiError) || !campo) {
      this.erroGeral.set(mensagemDeErro(e));
      return;
    }
    this.erros.set({ [campo]: TEXTOS_DE_CONFLITO.get(e.codigo) ?? e.mensagem });
    this.emailJaTemConta.set(e.codigo === "email_em_uso");
    const daEtapa1 = campo === "nome" || campo === "email" || campo === "senha";
    if (daEtapa1 && this.etapa() !== 1) {
      this.etapa.set(1);
      afterNextRender(() => this.focar(`k-${campo}`), { injector: this.injector });
    } else {
      this.focar(`k-${campo}`);
    }
  }

  /** Troca de etapa sem perder nada; o foco vai para o título da nova etapa. */
  private irPara(etapa: 1 | 2) {
    this.etapa.set(etapa);
    afterNextRender(() => this.focar(etapa === 1 ? "t-conta" : "t-empresa"), { injector: this.injector });
  }

  private limparErro(campo: Campo) {
    this.erroGeral.set("");
    if (campo === "email") this.emailJaTemConta.set(false);
    if (!this.erros()[campo]) return;
    this.erros.update((e) => {
      const resto = { ...e };
      delete resto[campo];
      return resto;
    });
  }

  private focar(id: string) {
    this.host.nativeElement.querySelector<HTMLElement>(`#${id}`)?.focus();
  }
}
