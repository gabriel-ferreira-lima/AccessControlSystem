import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { adminSession, operatorSession } from "../../test/fixtures";
import { renderWithProviders } from "../../test/render";
import { Topbar } from "./Topbar";

describe("Topbar", () => {
  it("mostra o rótulo do perfil do administrador", () => {
    renderWithProviders(<Topbar />, { route: "/", session: adminSession });

    expect(screen.getByText("Administrador")).toBeInTheDocument();
  });

  it("mostra o rótulo do perfil do operador", () => {
    renderWithProviders(<Topbar />, { route: "/", session: operatorSession });

    expect(screen.getByText("Operador")).toBeInTheDocument();
  });

  it("apaga a sessão e vai ao login ao encerrar a sessão", async () => {
    const { user } = renderWithProviders(<Topbar />, { route: "/", session: adminSession });

    await user.click(screen.getByRole("button", { name: "Encerrar sessao" }));

    expect(await screen.findByText("Rota atual: /login")).toBeInTheDocument();
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
  });

  it("oferece o botão de alternar o tema", () => {
    renderWithProviders(<Topbar />, { route: "/", session: adminSession });

    expect(screen.getByRole("button", { name: "Usar tema escuro" })).toBeInTheDocument();
  });
});
