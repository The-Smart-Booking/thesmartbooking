import { createBrowserRouter, redirect } from "react-router";
import App from "./App";
import Agendamentos from "./Agendamentos";
import NovoAgendamento from "./NovoAgendamento";

// handle.titulo vira a trilha da top-bar (App.tsx): "Agenda › Novo agendamento".
export const router = createBrowserRouter([
  {
    path: "/",
    Component: App,
    children: [
      { index: true, loader: () => redirect("/agendamentos") },
      {
        path: "agendamentos",
        handle: { titulo: "Agenda" },
        children: [
          { index: true, Component: Agendamentos },
          { path: "novo", Component: NovoAgendamento, handle: { titulo: "Novo agendamento" } },
        ],
      },
    ],
  },
]);
