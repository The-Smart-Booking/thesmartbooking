import { useEffect, useRef, useState, useSyncExternalStore } from "react";
import { Link, NavLink, Outlet, useMatches } from "react-router";

// Estrutura do DESIGN.md: celular com top-bar e gaveta (<dialog>), desktop com menu
// lateral fixo (o CSS troca a partir de 1024 px). O menu só lista telas que existem.

/** handle das rotas (route.ts): o título que aparece na trilha da top-bar. */
type Handle = { titulo: string };

type Tema = "claro" | "escuro";

// Sem escolha salva, vale o sistema. localStorage pode falhar (aba anônima): idem.
function lerTema(): Tema | null {
  try {
    const t = localStorage.getItem("tema");
    if (t === "claro" || t === "escuro") return t;
  } catch {
    /* segue o sistema */
  }
  return null;
}

// Tema do sistema, acompanhando a troca enquanto a página está aberta.
const escuroNoSistema = window.matchMedia("(prefers-color-scheme: dark)");
const assinarSistema = (avisar: () => void) => {
  escuroNoSistema.addEventListener("change", avisar);
  return () => escuroNoSistema.removeEventListener("change", avisar);
};

function App() {
  const gaveta = useRef<HTMLDialogElement>(null);
  const [escolhido, setEscolhido] = useState(lerTema);
  const sistemaEscuro = useSyncExternalStore(assinarSistema, () => escuroNoSistema.matches);
  const tema: Tema = escolhido ?? (sistemaEscuro ? "escuro" : "claro");
  const trilha = useMatches().filter((m) => (m.handle as Handle | undefined)?.titulo);
  const titulo = (trilha.at(-1)?.handle as Handle | undefined)?.titulo;

  function trocarTema() {
    const novo: Tema = tema === "escuro" ? "claro" : "escuro";
    setEscolhido(novo);
    document.documentElement.dataset.tema = novo; // o index.html faz o mesmo ao carregar
    try {
      localStorage.setItem("tema", novo);
    } catch {
      /* sem localStorage, a escolha vale só até recarregar */
    }
  }

  useEffect(() => {
    document.title = titulo ? `${titulo} · Smart Booking` : "Smart Booking";
  }, [titulo]);

  const fechar = () => gaveta.current?.close();

  return (
    <div className="app">
      <aside className="lateral">
        <Logo />
        <Menu />
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
          <BotaoTema tema={tema} onClick={trocarTema} />
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

/** Botão redondo da top-bar: o ícone mostra o tema em uso; o clique troca. */
function BotaoTema({ tema, onClick }: { tema: Tema; onClick: () => void }) {
  const rotulo = tema === "escuro" ? "Tema escuro. Mudar para o claro" : "Tema claro. Mudar para o escuro";
  return (
    <button type="button" className="botao-tema" aria-label={rotulo} title={rotulo} onClick={onClick}>
      {tema === "escuro" ? (
        <svg width="18" height="18" viewBox="0 0 24 24" aria-hidden="true">
          <path d="M20 14.5A8 8 0 0 1 9.5 4a8 8 0 1 0 10.5 10.5z" fill="currentColor" />
        </svg>
      ) : (
        <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
          <circle cx="12" cy="12" r="4" fill="currentColor" />
          <path
            d="M12 2.5v2M12 19.5v2M2.5 12h2M19.5 12h2M5.3 5.3l1.4 1.4M17.3 17.3l1.4 1.4M5.3 18.7l1.4-1.4M17.3 6.7l1.4-1.4"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinecap="round"
          />
        </svg>
      )}
    </button>
  );
}

export default App;
