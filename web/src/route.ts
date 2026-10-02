import { createBrowserRouter, redirect } from "react-router";
import App from "./App";
import Agendamentos from "./Agendamentos";
import NovoAgendamento from "./NovoAgendamento";

export const router = createBrowserRouter([
  {
    path: "/",
    Component: App,
    children: [
      { index: true, loader: () => redirect("/agendamentos") },
      { path: "agendamentos", Component: Agendamentos },
      { path: "agendamentos/novo", Component: NovoAgendamento },
    ],
  },
]);
