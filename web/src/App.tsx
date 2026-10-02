import { NavLink, Outlet } from "react-router";

function App() {
  return (
    <>
      <nav>
        <NavLink to="/agendamentos" end>
          Agendamentos
        </NavLink>
        <NavLink to="/agendamentos/novo">Novo agendamento</NavLink>
      </nav>
      <main>
        <Outlet />
      </main>
    </>
  );
}

export default App;
