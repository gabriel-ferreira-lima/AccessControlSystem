import { Outlet } from "react-router-dom";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

export function AppShell() {
  return (
    <div className="app-shell">
      <a className="skip-link" href="#conteudo">
        Ir para o conteudo
      </a>
      <Sidebar />
      <div className="app-shell__main">
        <Topbar />
        <main id="conteudo" className="app-shell__content" tabIndex={-1}>
          <Outlet />
        </main>
      </div>
    </div>
  );
}
