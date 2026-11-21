import { NgTemplateOutlet } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  linkedSignal,
  signal,
} from "@angular/core";
import { rxResource } from "@angular/core/rxjs-interop";
import { ActivatedRoute } from "@angular/router";
import { firstValueFrom } from "rxjs";
import { Api, ApiError, ErroRede, mensagemDeErro } from "../api";
import {
  dataLonga,
  diaCurto,
  diaDaSemana,
  diaEmFrase,
  diaLongo,
  dinheiro,
  duracaoEmTexto,
  horaEmFrase,
  iniciais,
  instanteNoFuso,
  juntar,
  noFuso,
  nomeDoMes,
  numeroDoDia,
  somarDias,
} from "../datas";
import { MensagemErro, Vazio } from "../estados";
import { Logo } from "../layout";

// Página do cliente (padrão C dentro do padrão F): serviço, com quem, dia, horário e os dados dele.
// Dia e hora são sempre os do fuso da empresa, nunca os do navegador. A regra de atendimento é a
// mesma do backend (segunda a sábado, das 08:00 às 19:00); o 409 continua sendo a palavra final.

const PRIMEIRO_MINUTO = 8 * 60;
const ULTIMO_MINUTO = 19 * 60;
const PASSO = 30;
const DIAS_NA_FILA = 8;
const DOMINGO = 0;
const ATUALIZAR_RELOGIO_MS = 30_000;

interface Horario {
  hora: string; // "09:30"
  fim: string; // "10:00"
  ocupado: boolean;
}

/**
 * DDD + número, só dígitos e sem o 55 do país: é o formato que o backend compara para reaproveitar
 * o cadastro. Devolve "" se não for um telefone do Brasil.
 */
function normalizarTelefone(t: string): string {
  const d = t.replace(/\D/g, "").replace(/^55(?=\d{10,11}$)/, "");
  const valido = /^[1-9]{2}(9\d{8}|[2-9]\d{7})$/.test(d) && !/^(\d)\1+$/.test(d.slice(2));
  return valido ? d : "";
}

@Component({
  selector: "app-agendar",
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [NgTemplateOutlet, MensagemErro, Vazio, Logo],
  templateUrl: "./agendar.html",
})
export class Agendar {
  private readonly api = inject(Api);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly slug = inject(ActivatedRoute).snapshot.paramMap.get("slug") ?? "";

  /** Relógio da tela: horário que vence com a página aberta deixa de ser oferecido. */
  private readonly agora = signal(Date.now());

  constructor() {
    const relogio = setInterval(() => this.agora.set(Date.now()), ATUALIZAR_RELOGIO_MS);
    inject(DestroyRef).onDestroy(() => clearInterval(relogio));
  }

  // ── Dados da empresa ────────────────────────────────────────
  protected readonly empresa = rxResource({
    params: () => this.slug || undefined,
    stream: ({ params }) => this.api.empresaPublica(params),
  });
  protected readonly servicos = rxResource({
    params: () => (this.empresa.hasValue() ? this.slug : undefined),
    stream: ({ params }) => this.api.servicosPublicos(params),
  });
  protected readonly prestadores = rxResource({
    params: () => (this.empresa.hasValue() ? this.slug : undefined),
    stream: ({ params }) => this.api.prestadoresPublicos(params),
  });

  private readonly erroBase = computed(
    () => this.empresa.error() ?? this.servicos.error() ?? this.prestadores.error() ?? null,
  );
  protected readonly naoEncontrada = computed(() => {
    const e = this.erroBase();
    return !this.slug || (e instanceof ApiError && e.codigo === "nao_encontrado");
  });
  protected readonly semConexao = computed(() => this.erroBase() instanceof ErroRede);
  protected readonly mensagemErroBase = computed(() => (this.erroBase() ? mensagemDeErro(this.erroBase()) : ""));
  protected readonly carregandoBase = computed(
    () => !this.empresa.hasValue() || !this.servicos.hasValue() || !this.prestadores.hasValue(),
  );

  protected tentarDeNovo() {
    this.empresa.reload();
    this.servicos.reload();
    this.prestadores.reload();
  }

  protected readonly nomeEmpresa = computed(() => (this.empresa.hasValue() ? this.empresa.value().nome : ""));
  protected readonly contatoEmpresa = computed(() => (this.empresa.hasValue() ? this.empresa.value().contato : ""));
  private readonly fuso = computed(() => (this.empresa.hasValue() ? this.empresa.value().fuso_horario : ""));
  protected readonly hoje = computed(() => (this.fuso() ? noFuso(this.agora(), this.fuso()).dia : ""));

  // ── Serviço e profissional ──────────────────────────────────
  protected readonly servicoId = signal("");
  protected readonly servico = computed(
    () => (this.servicos.hasValue() ? this.servicos.value() : []).find((s) => s.id === this.servicoId()) ?? null,
  );

  protected readonly listaServicos = computed(() =>
    (this.servicos.hasValue() ? this.servicos.value() : []).map((s) => ({
      ...s,
      duracao: duracaoEmTexto(s.duracao_minutos),
      preco: dinheiro(s.preco_centavos).replace(",00", ""),
    })),
  );

  protected readonly prestadorIdEscolhido = signal("");
  protected readonly listaPrestadores = computed(() =>
    (this.prestadores.hasValue() ? this.prestadores.value() : []).map((p) => ({ ...p, iniciais: iniciais(p.nome) })),
  );
  /** Com um só profissional, ele já vem escolhido. */
  protected readonly prestador = computed(() => {
    const lista = this.listaPrestadores();
    if (lista.length === 1) return lista[0];
    return lista.find((p) => p.id === this.prestadorIdEscolhido()) ?? null;
  });
  private readonly prestadorId = computed(() => this.prestador()?.id ?? "");

  // ── Dia ─────────────────────────────────────────────────────
  /** Hoje ou, no domingo, a segunda: domingo não é oferecido. */
  private readonly primeiroDia = computed(() => {
    const hoje = this.hoje();
    return hoje && diaDaSemana(hoje) === DOMINGO ? somarDias(hoje, 1) : hoje;
  });
  protected readonly paginaDaFila = signal(0);
  /** O dia escolhido vale enquanto não passar; virou o dia com a página aberta, volta ao primeiro. */
  protected readonly dia = linkedSignal<string, string>({
    source: this.primeiroDia,
    computation: (primeiro, anterior) => (anterior && anterior.value >= primeiro ? anterior.value : primeiro),
  });

  protected readonly fila = computed(() => {
    const primeiro = this.primeiroDia();
    if (!primeiro) return [];
    const dias: string[] = [];
    let pular = this.paginaDaFila() * DIAS_NA_FILA;
    for (let d = primeiro; dias.length < DIAS_NA_FILA; d = somarDias(d, 1)) {
      if (diaDaSemana(d) === DOMINGO) continue;
      if (pular-- > 0) continue;
      dias.push(d);
    }
    return dias.map((dia) => ({
      dia,
      curto: diaCurto(dia),
      numero: numeroDoDia(dia),
      hoje: dia === this.hoje(),
      rotulo: dataLonga(dia),
    }));
  });

  protected readonly mesDaFila = computed(() => {
    const fila = this.fila();
    if (!fila.length) return "";
    const primeiro = nomeDoMes(fila[0].dia);
    const ultimo = nomeDoMes(fila[fila.length - 1].dia);
    return primeiro === ultimo ? primeiro : `${primeiro} e ${ultimo.toLowerCase()}`;
  });

  // ── Horário ─────────────────────────────────────────────────
  /** Trocar o dia ou o profissional desfaz a escolha do horário. */
  protected readonly hora = linkedSignal<string, string>({
    source: () => `${this.dia()}|${this.prestadorId()}`,
    computation: () => "",
  });

  protected readonly ocupados = rxResource({
    params: () =>
      this.prestadorId() && this.fuso()
        ? { dia: this.dia(), prestadorId: this.prestadorId(), fuso: this.fuso() }
        : undefined,
    stream: ({ params: { dia, prestadorId, fuso } }) =>
      this.api.ocupadosPublicos(
        this.slug,
        new Date(instanteNoFuso(dia, "00:00", fuso)).toISOString(),
        new Date(instanteNoFuso(somarDias(dia, 1), "00:00", fuso)).toISOString(),
        prestadorId,
      ),
  });
  protected readonly erroDoDia = computed(() => (this.ocupados.error() ? mensagemDeErro(this.ocupados.error()) : ""));

  /** Horários do dia para a duração do serviço; os que já passaram nem aparecem. */
  protected readonly horarios = computed<Horario[]>(() => {
    const s = this.servico();
    const fuso = this.fuso();
    if (!s || !this.ocupados.hasValue()) return [];
    const ocupados = this.ocupados.value().map((o) => [Date.parse(o.inicio), Date.parse(o.fim)] as const);
    const lista: Horario[] = [];
    for (let m = PRIMEIRO_MINUTO; m + s.duracao_minutos <= ULTIMO_MINUTO; m += PASSO) {
      const hora = `${String(Math.floor(m / 60)).padStart(2, "0")}:${String(m % 60).padStart(2, "0")}`;
      const inicio = instanteNoFuso(this.dia(), hora, fuso);
      if (inicio <= this.agora()) continue;
      const fim = inicio + s.duracao_minutos * 60_000;
      const ocupado = ocupados.some(([i, f]) => inicio < f && fim > i);
      lista.push({ hora, fim: noFuso(fim, fuso).hora, ocupado });
    }
    return lista;
  });

  protected readonly manha = computed(() => this.horarios().filter((h) => h.hora < "12:00"));
  protected readonly tarde = computed(() => this.horarios().filter((h) => h.hora >= "12:00"));
  protected readonly livres = computed(() => this.horarios().filter((h) => !h.ocupado));

  /** Horário escolhido, se ainda estiver livre (o tempo passa e trocar o serviço pode ocupá-lo). */
  protected readonly escolhido = computed(() => this.livres().find((h) => h.hora === this.hora()) ?? null);

  /** Hora que estava escolhida e deixou de valer: venceu com a página aberta ou não cabe no serviço. */
  protected readonly horaPerdida = computed(() =>
    this.hora() && this.servico() && this.ocupados.hasValue() && !this.escolhido() ? this.hora() : "",
  );

  protected readonly legendaHorarios = computed(() => {
    const s = this.servico();
    const p = this.prestador();
    if (!s || !p || !this.dia()) return "";
    const quando =
      this.dia() === this.hoje() ? "hoje" : `${/^(sábado|domingo)/.test(diaLongo(this.dia())) ? "no" : "na"} ${diaLongo(this.dia())}`;
    return `Livres ${quando} com ${p.nome.split(" ")[0]}, para ${duracaoEmTexto(s.duracao_minutos)}`;
  });

  // ── Seus dados ──────────────────────────────────────────────
  protected readonly nome = signal("");
  protected readonly whatsapp = signal("");
  protected readonly nomeTocado = signal(false);
  protected readonly whatsappTocado = signal(false);
  /** Recusas do backend, mostradas no campo até a pessoa corrigir. */
  protected readonly recusaNome = signal("");
  protected readonly recusaWhatsapp = signal("");
  private readonly nomeValido = computed(() => this.nome().trim().length >= 2);
  private readonly telefone = computed(() => normalizarTelefone(this.whatsapp()));

  protected readonly erroNome = computed(
    () => this.recusaNome() || (this.nomeTocado() && !this.nomeValido() ? "Escreva seu nome." : ""),
  );
  protected readonly erroWhatsapp = computed(
    () =>
      this.recusaWhatsapp() ||
      (this.whatsappTocado() && !this.telefone() ? "Use DDD + número, como (11) 98877-4410." : ""),
  );

  // ── Resumo e envio ──────────────────────────────────────────
  /** O que falta, na ordem da tela; `alvo` é o id que recebe o foco. */
  private readonly pendencias = computed(() => {
    const lista: { alvo: string; texto: string }[] = [];
    if (!this.servico()) lista.push({ alvo: "secao-servico", texto: "o serviço" });
    if (!this.prestador()) lista.push({ alvo: "secao-prestador", texto: "o profissional" });
    if (!this.escolhido()) lista.push({ alvo: "secao-horario", texto: "o horário" });
    if (!this.nomeValido() || this.recusaNome()) lista.push({ alvo: "nome", texto: "seu nome" });
    if (!this.telefone() || this.recusaWhatsapp()) lista.push({ alvo: "whatsapp", texto: "o WhatsApp com DDD" });
    return lista;
  });
  protected readonly falta = computed(() => {
    const textos = this.pendencias().map((p) => p.texto);
    return textos.length ? `Para agendar, falta ${juntar(textos)}.` : "";
  });

  /** "Agendar quarta, 10h" (hoje, amanhã, dia da semana ou a data). */
  protected readonly rotuloBotao = computed(() => {
    const h = this.escolhido();
    if (!h || !this.servico() || !this.prestador()) return "Agendar";
    return `Agendar ${diaEmFrase(this.dia(), this.hoje())}, ${horaEmFrase(h.hora)}`;
  });

  protected readonly enviando = signal(false);
  protected readonly erro = signal("");
  /** Recusa do horário pelo backend (400 em `inicio`): volta para a escolha com o aviso. */
  protected readonly recusaHorario = signal("");
  /** Horário que a API recusou com 409; as alternativas saem da lista recarregada. */
  protected readonly horaRecusada = signal("");
  protected readonly alternativas = computed(() => {
    const recusada = this.horaRecusada();
    if (!recusada) return [];
    const depois = this.livres().filter((h) => h.hora > recusada);
    return (depois.length ? depois : this.livres()).slice(0, 2);
  });
  /** Depois de um 429, novo envio só quando acabar a espera pedida pela API. */
  private bloqueadoAte = 0;

  /** Depois de agendar, a confirmação toma o lugar do formulário. */
  protected readonly agendado = signal<{ frase: string; detalhe: string } | null>(null);

  protected readonly dataLonga = dataLonga;
  protected readonly dinheiro = dinheiro;
  protected readonly duracaoEmTexto = duracaoEmTexto;
  protected readonly iniciais = iniciais;

  protected escolherServico(id: string) {
    this.servicoId.set(id);
    this.limparAvisos();
  }

  protected escolherPrestador(id: string) {
    this.prestadorIdEscolhido.set(id);
    this.limparAvisos();
  }

  protected andarFila(direcao: -1 | 1) {
    this.paginaDaFila.update((p) => Math.max(0, p + direcao));
  }

  protected escolherDia(dia: string) {
    this.dia.set(dia);
    this.limparAvisos();
  }

  protected escolherHora(hora: string) {
    this.hora.set(hora);
    this.limparAvisos();
  }

  protected digitarNome(valor: string) {
    this.nome.set(valor);
    this.recusaNome.set("");
  }

  protected digitarWhatsapp(valor: string) {
    this.whatsapp.set(valor);
    this.recusaWhatsapp.set("");
  }

  private limparAvisos() {
    this.erro.set("");
    this.horaRecusada.set("");
    this.recusaHorario.set("");
  }

  /** Leva o foco ao campo ou à primeira opção da seção; sem opção, à própria seção. */
  private focar(id: string) {
    const alvo = this.host.nativeElement.querySelector<HTMLElement>(`#${id}`);
    const campo = alvo?.matches("input") ? alvo : alvo?.querySelector<HTMLElement>("button:not(:disabled)");
    (campo ?? alvo)?.focus();
  }

  protected async agendar(e: SubmitEvent) {
    e.preventDefault();
    if (this.enviando()) return;
    const restante = Math.ceil((this.bloqueadoAte - Date.now()) / 1000);
    if (restante > 0) {
      this.erro.set(mensagemDeErro(new ApiError(429, "muitas_tentativas", "", undefined, restante)));
      return;
    }
    this.agora.set(Date.now()); // o horário pode ter vencido desde o último tique do relógio
    this.nomeTocado.set(true);
    this.whatsappTocado.set(true);
    const [pendente] = this.pendencias();
    const s = this.servico();
    const p = this.prestador();
    const h = this.escolhido();
    if (pendente || !s || !p || !h) {
      if (pendente) this.focar(pendente.alvo);
      return;
    }
    const dia = this.dia();
    const fuso = this.fuso();
    this.limparAvisos();
    this.enviando.set(true); // aria-busy no botão e sem segundo POST
    try {
      const criado = await firstValueFrom(
        this.api.criarAgendamentoPublico(this.slug, {
          servico_id: s.id,
          prestador_id: p.id,
          inicio: `${dia}T${h.hora}:00`,
          nome: this.nome().trim(),
          telefone: this.telefone(),
        }),
      );
      // A confirmação mostra o que a API gravou, no fuso da empresa.
      const inicio = noFuso(criado.inicio, fuso);
      const fim = noFuso(criado.fim, fuso);
      this.agendado.set({
        frase: `Combinado. Até ${diaEmFrase(inicio.dia, this.hoje())}, às ${horaEmFrase(inicio.hora)}.`,
        detalhe: `${s.nome} com ${p.nome} · ${dataLonga(inicio.dia)}, ${inicio.hora} às ${fim.hora} · ${dinheiro(criado.valor_centavos)}, pago no local`,
      });
    } catch (err) {
      this.tratarRecusa(err, h.hora);
    }
    this.enviando.set(false);
  }

  private tratarRecusa(err: unknown, hora: string) {
    const codigo = err instanceof ApiError ? err.codigo : "";
    const campo = err instanceof ApiError ? err.campo : undefined;
    if (codigo === "conflito_horario") {
      this.horaRecusada.set(hora);
      this.hora.set("");
      this.ocupados.reload(); // o horário ocupado aparece riscado e as alternativas se atualizam
    } else if (codigo === "requisicao_invalida" && campo === "inicio") {
      this.recusaHorario.set(mensagemDeErro(err));
      this.hora.set("");
      this.ocupados.reload();
      this.host.nativeElement.querySelector<HTMLElement>("#secao-horario")?.focus();
    } else if (codigo === "requisicao_invalida" && campo === "nome") {
      this.recusaNome.set(mensagemDeErro(err));
      this.focar("nome");
    } else if (codigo === "requisicao_invalida" && campo === "telefone") {
      this.recusaWhatsapp.set(mensagemDeErro(err));
      this.focar("whatsapp");
    } else if (codigo === "nao_encontrado" || campo === "servico_id" || campo === "prestador_id") {
      // Serviço ou profissional saiu do ar: as listas voltam sem ele e o botão diz o que escolher.
      this.erro.set(mensagemDeErro(err));
      this.servicos.reload();
      this.prestadores.reload();
    } else {
      if (codigo === "muitas_tentativas" && err instanceof ApiError && err.esperaSegundos) {
        this.bloqueadoAte = Date.now() + err.esperaSegundos * 1000;
      }
      this.erro.set(mensagemDeErro(err));
    }
  }

  /** Marcar outro horário depois de confirmar: mantém nome e WhatsApp. */
  protected marcarOutro() {
    this.agendado.set(null);
    this.hora.set("");
    this.ocupados.reload();
  }
}
