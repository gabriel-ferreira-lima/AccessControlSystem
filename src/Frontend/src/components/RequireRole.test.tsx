import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { adminSession, operatorSession } from "../test/fixtures";
import { renderWithProviders } from "../test/render";
import type { Role } from "../types/api";
import { RequireRole } from "./RequireRole";

// A rota de teste nunca é "/": o destino do redirecionamento é "/", e a tela montada em "/" entraria em laço.
function renderGuard(role: Role, session?: typeof adminSession) {
  return renderWithProviders(
    <RequireRole role={role}>
      <p>conteúdo restrito</p>
    </RequireRole>,
    { route: "/restrita", session },
  );
}

describe("RequireRole", () => {
  it("renderiza os filhos quando o perfil da sessão é o exigido (Admin)", () => {
    renderGuard("Admin", adminSession);

    expect(screen.getByText("conteúdo restrito")).toBeInTheDocument();
  });

  it("renderiza os filhos quando o perfil da sessão é o exigido (Operator)", () => {
    renderGuard("Operator", operatorSession);

    expect(screen.getByText("conteúdo restrito")).toBeInTheDocument();
  });

  it("redireciona o Operador para o início quando a rota exige Admin", () => {
    renderGuard("Admin", operatorSession);

    expect(screen.getByText("Rota atual: /")).toBeInTheDocument();
    expect(screen.queryByText("conteúdo restrito")).not.toBeInTheDocument();
  });

  it("redireciona o Administrador para o início quando a rota exige Operator", () => {
    renderGuard("Operator", adminSession);

    expect(screen.getByText("Rota atual: /")).toBeInTheDocument();
    expect(screen.queryByText("conteúdo restrito")).not.toBeInTheDocument();
  });

  it("redireciona para o início quando não há sessão", () => {
    renderGuard("Admin");

    expect(screen.getByText("Rota atual: /")).toBeInTheDocument();
    expect(screen.queryByText("conteúdo restrito")).not.toBeInTheDocument();
  });
});
