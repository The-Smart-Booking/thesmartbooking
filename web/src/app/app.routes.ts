import { Injectable, inject } from "@angular/core";
import { Title } from "@angular/platform-browser";
import { type RouterStateSnapshot, type Routes, TitleStrategy } from "@angular/router";
import { Agendamentos } from "./agendamentos/agendamentos";
import { Agendar } from "./agendar/agendar";
import { Cadastro } from "./cadastro/cadastro";
import { Entrar } from "./entrar/entrar";
import { Painel, trilhaDe } from "./layout";
import { NovoAgendamento } from "./novo-agendamento/novo-agendamento";
import { semSessaoGuard, sessaoGuard } from "./sessao";

// data.titulo vira a trilha da top-bar (layout.ts): "Agenda › Novo agendamento".
export const routes: Routes = [
  // Página do cliente, aberta pelo link da empresa: fora da casca do painel, sem menu nem login.
  // Sem slug válido, a própria página avisa que a empresa não foi encontrada.
  {
    path: "agendar",
    data: { titulo: "Agendar" },
    children: [
      { path: "", component: Agendar },
      { path: ":slug", component: Agendar },
      { path: "**", component: Agendar },
    ],
  },
  // A primeira página do app é o login; com sessão, o semSessaoGuard leva de lá à agenda.
  { path: "", pathMatch: "full", redirectTo: "entrar" },
  // Entrar e Cadastro (#120): fora da casca do painel.
  { path: "entrar", component: Entrar, canActivate: [semSessaoGuard], data: { titulo: "Entrar" } },
  { path: "cadastro", component: Cadastro, data: { titulo: "Cadastro" } },
  // Painel só com sessão (sessaoGuard). O "**" é filho daqui: rota inexistente sem sessão
  // vai para /entrar; com sessão, para a agenda.
  {
    path: "",
    component: Painel,
    canActivate: [sessaoGuard],
    children: [
      {
        path: "agendamentos",
        data: { titulo: "Agenda" },
        children: [
          { path: "", component: Agendamentos },
          { path: "novo", component: NovoAgendamento, data: { titulo: "Novo agendamento" } },
        ],
      },
      { path: "**", redirectTo: "agendamentos" },
    ],
  },
];

/** document.title = "<último da trilha> · Dayspot". */
@Injectable()
export class TituloDayspot extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(estado: RouterStateSnapshot) {
    const titulo = trilhaDe(estado.root).at(-1)?.titulo;
    this.title.setTitle(titulo ? `${titulo} · Dayspot` : "Dayspot");
  }
}
