import { render } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ReactElement } from "react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import App from "../App";
import { AuthProvider } from "../context/AuthContext";
import { ThemeProvider } from "../context/ThemeContext";
import type { Session } from "../types/api";

/**
 * Grava a sessão na chave literal `acs.session`, de propósito (e não via `writeSession`): se a chave
 * for renomeada em src/lib/session-storage.ts, os testes de página quebram junto com o da própria chave.
 */
export function storeSession(session: Session): void {
  window.sessionStorage.setItem("acs.session", JSON.stringify(session));
}

/** Destino de qualquer navegação que não seja a rota da tela: mostra onde o teste foi parar. */
function CurrentRoute() {
  const location = useLocation();
  return (
    <p>
      Rota atual: {location.pathname}
      {location.search}
    </p>
  );
}

export interface RenderWithProvidersOptions {
  /** Rota real da tela (padrão "/"); `?busca` entra na entrada inicial, não no `path` da rota. */
  route?: string;
  /** `location.state` da entrada inicial (ex.: e-mail e perfil vindos da lista). */
  routeState?: unknown;
  /** Sessão gravada em `sessionStorage` antes de renderizar. */
  session?: Session;
}

/**
 * Renderiza com os providers reais (ThemeProvider, AuthProvider, MemoryRouter). A `ui` é montada
 * no `pathname` de `route`; qualquer outro destino cai numa rota coringa que mostra
 * `Rota atual: <caminho>`. Por isso `route` deve ser a rota real da tela, e nunca igual ao destino
 * do redirecionamento testado.
 */
export function renderWithProviders(ui: ReactElement, options: RenderWithProvidersOptions = {}) {
  const { route = "/", routeState, session } = options;
  if (session) {
    storeSession(session);
  }

  const url = new URL(route, "http://localhost");
  const result = render(
    <ThemeProvider>
      <AuthProvider>
        <MemoryRouter
          initialEntries={[{ pathname: url.pathname, search: url.search, state: routeState }]}
        >
          <Routes>
            <Route path={url.pathname} element={ui} />
            <Route path="*" element={<CurrentRoute />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>
    </ThemeProvider>,
  );

  return { ...result, user: userEvent.setup() };
}

export interface RenderAppOptions {
  /** Endereço aberto antes de renderizar (padrão "/"). */
  path?: string;
  session?: Session;
}

/**
 * Renderiza o `App` real (que já traz os próprios providers e o BrowserRouter) depois de
 * `history.pushState` em `path`. Usado nos testes de navegação e de jornada.
 */
export function renderApp(options: RenderAppOptions = {}) {
  const { path = "/", session } = options;
  if (session) {
    storeSession(session);
  }
  window.history.pushState({}, "", path);

  const result = render(<App />);
  return { ...result, user: userEvent.setup() };
}
