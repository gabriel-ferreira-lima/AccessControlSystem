import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { AuthProvider } from "../context/AuthContext";
import { adminSession } from "../test/fixtures";
import { renderWithProviders, storeSession } from "../test/render";
import { RequireSession } from "./RequireSession";

describe("RequireSession", () => {
  it("renderiza os filhos quando há sessão", () => {
    renderWithProviders(
      <RequireSession>
        <p>conteúdo protegido</p>
      </RequireSession>,
      { route: "/protegida", session: adminSession },
    );

    expect(screen.getByText("conteúdo protegido")).toBeInTheDocument();
  });

  it("redireciona para o login quando não há sessão", () => {
    renderWithProviders(
      <RequireSession>
        <p>conteúdo protegido</p>
      </RequireSession>,
      { route: "/protegida" },
    );

    expect(screen.getByText("Rota atual: /login")).toBeInTheDocument();
    expect(screen.queryByText("conteúdo protegido")).not.toBeInTheDocument();
  });

  it("entrega a rota de origem em state.from ao redirecionar para o login", () => {
    // Rota própria no /login para ler o location.state, que o helper de render não exibe.
    function LoginProbe() {
      const location = useLocation();
      const from = (location.state as { from?: string } | null)?.from;
      return <p>origem: {from ?? "nenhuma"}</p>;
    }

    render(
      <AuthProvider>
        <MemoryRouter initialEntries={["/operadores/editar?id=1"]}>
          <Routes>
            <Route
              path="/operadores/editar"
              element={
                <RequireSession>
                  <p>conteúdo protegido</p>
                </RequireSession>
              }
            />
            <Route path="/login" element={<LoginProbe />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    expect(screen.getByText("origem: /operadores/editar")).toBeInTheDocument();
  });

  it("usa a sessão guardada no sessionStorage antes de renderizar", () => {
    storeSession(adminSession);

    renderWithProviders(
      <RequireSession>
        <p>conteúdo protegido</p>
      </RequireSession>,
      { route: "/protegida" },
    );

    expect(screen.getByText("conteúdo protegido")).toBeInTheDocument();
  });
});
