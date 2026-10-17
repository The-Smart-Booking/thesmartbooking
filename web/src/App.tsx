import { useEffect, useId, useRef, useState } from "react";
import { Link, NavLink, Outlet, useMatches } from "react-router";

// Estrutura do DESIGN.md: celular com top-bar e gaveta (<dialog>), desktop com menu
// lateral fixo (o CSS troca a partir de 1024 px). O menu só lista telas que existem.

/** handle das rotas (route.ts): o título que aparece na trilha da top-bar. */
type Handle = { titulo: string };

type Tema = "sistema" | "claro" | "escuro";
const temas: [Tema, string][] = [
  ["sistema", "Sistema"],
  ["claro", "Claro"],
  ["escuro", "Escuro"],
];

// localStorage pode falhar (aba anônima, cookies bloqueados): aí vale o sistema.
function lerTema(): Tema {
  try {
    const t = localStorage.getItem("tema");
    if (t === "claro" || t === "escuro") return t;
  } catch {
    /* segue o sistema */
  }
  return "sistema";
}

function App() {
  const gaveta = useRef<HTMLDialogElement>(null);
  const [tema, setTema] = useState(lerTema);
  const trilha = useMatches().filter((m) => (m.handle as Handle | undefined)?.titulo);
  const titulo = (trilha.at(-1)?.handle as Handle | undefined)?.titulo;

  // O index.html já aplicou o tema salvo antes de montar; aqui só acompanha a troca.
  useEffect(() => {
    const raiz = document.documentElement;
    if (tema === "sistema") delete raiz.dataset.tema;
    else raiz.dataset.tema = tema;
    try {
      if (tema === "sistema") localStorage.removeItem("tema");
      else localStorage.setItem("tema", tema);
    } catch {
      /* sem localStorage, a escolha vale só até recarregar */
    }
  }, [tema]);

  useEffect(() => {
    document.title = titulo ? `${titulo} · Smart Booking` : "Smart Booking";
  }, [titulo]);

  const fechar = () => gaveta.current?.close();
  const seletor = <SeletorTema tema={tema} onChange={setTema} />;

  return (
    <div className="app">
      <aside className="lateral">
        <Logo />
        <Menu />
        {seletor}
      </aside>

      <dialog
        ref={gaveta}
        className="gaveta"
        aria-label="Menu"
        // Clique no fundo escurecido (fora do conteúdo) fecha a gaveta.
        onClick={(e) => e.target === e.currentTarget && fechar()}
      >
        <div className="gaveta-topo">
          <Logo />
          <button type="button" className="icone" aria-label="Fechar menu" onClick={fechar}>
            <svg width="20" height="20" viewBox="0 0 24 24" aria-hidden="true">
              <path d="M6 6l12 12M18 6L6 18" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </button>
        </div>
        <Menu onNavegar={fechar} />
        {seletor}
      </dialog>

      <div>
        <header className="topo">
          <button
            type="button"
            className="icone abrir-menu"
            aria-label="Abrir menu"
            onClick={() => gaveta.current?.showModal()}
          >
            <svg width="22" height="22" viewBox="0 0 24 24" aria-hidden="true">
              <path d="M4 7h16M4 12h16M4 17h16" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
            </svg>
          </button>
          <nav className="trilha" aria-label="Você está em">
            {trilha.map((m, i) =>
              i < trilha.length - 1 ? (
                <span key={m.id}>
                  <Link to={m.pathname}>{(m.handle as Handle).titulo}</Link> ›
                </span>
              ) : (
                <h1 key={m.id}>{(m.handle as Handle).titulo}</h1>
              ),
            )}
          </nav>
        </header>
        <main className="conteudo">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

/** Símbolo (grade de horários com a pílula reservada) e nome. Sobre a nav: pontos brancos. */
function Logo() {
  const pontos = [
    [22, 22],
    [60, 22],
    [98, 22],
    [22, 60],
    [22, 98],
    [60, 98],
    [98, 98],
  ];
  return (
    <div className="logo">
      <svg width="28" height="28" viewBox="0 0 120 120" aria-hidden="true">
        {pontos.map(([x, y]) => (
          <circle key={`${x}-${y}`} cx={x} cy={y} r="11" fill="#ffffff" />
        ))}
        <rect x="49" y="49" width="60" height="22" rx="11" fill="var(--marca)" />
      </svg>
      <span>
        smart<b>booking</b>
      </span>
    </div>
  );
}

function Menu({ onNavegar }: { onNavegar?: () => void }) {
  return (
    <nav className="menu" aria-label="Menu principal">
      <NavLink to="/agendamentos" onClick={onNavegar}>
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <rect x="3.5" y="5" width="17" height="15" rx="2.5" stroke="currentColor" strokeWidth="1.8" />
          <path d="M3.5 10h17M8 3v4M16 3v4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
        Agenda
      </NavLink>
    </nav>
  );
}

/** Segmento Sistema · Claro · Escuro com rádios nativos: setas, foco e leitor de tela de graça. */
function SeletorTema({ tema, onChange }: { tema: Tema; onChange: (t: Tema) => void }) {
  const nome = useId(); // dois seletores na página (lateral e gaveta): cada grupo com o seu name
  return (
    <fieldset className="tema">
      <legend>Tema</legend>
      {temas.map(([valor, rotulo]) => (
        <label key={valor}>
          <input type="radio" name={nome} value={valor} checked={tema === valor} onChange={() => onChange(valor)} />
          {rotulo}
        </label>
      ))}
    </fieldset>
  );
}

export default App;
