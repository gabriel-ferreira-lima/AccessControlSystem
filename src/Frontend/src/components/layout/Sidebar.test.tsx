import { screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { adminSession, operatorSession } from "../../test/fixtures";
import { renderWithProviders } from "../../test/render";
import { Sidebar } from "./Sidebar";

function navigation() {
  return screen.getByRole("navigation", { name: "Navegação principal" });
}

describe("Sidebar", () => {
  it("mostra para o Administrador os itens Início e Operadores, sem Minha conta", () => {
    renderWithProviders(<Sidebar />, { route: "/", session: adminSession });

    const links = within(navigation()).getAllByRole("link");
    expect(links.map((link) => link.textContent)).toEqual(["Início", "Operadores"]);
    expect(within(navigation()).queryByRole("link", { name: "Minha conta" })).not.toBeInTheDocument();
  });

  it("mostra para o Operador os itens Início e Minha conta, sem Operadores", () => {
    renderWithProviders(<Sidebar />, { route: "/", session: operatorSession });

    const links = within(navigation()).getAllByRole("link");
    expect(links.map((link) => link.textContent)).toEqual(["Início", "Minha conta"]);
    expect(within(navigation()).queryByRole("link", { name: "Operadores" })).not.toBeInTheDocument();
  });

  it("mostra só os itens públicos quando não há sessão", () => {
    renderWithProviders(<Sidebar />, { route: "/" });

    const links = within(navigation()).getAllByRole("link");
    expect(links.map((link) => link.textContent)).toEqual(["Início"]);
  });

  it("aponta cada item para a sua rota", () => {
    renderWithProviders(<Sidebar />, { route: "/", session: adminSession });

    expect(screen.getByRole("link", { name: "Início" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "Operadores" })).toHaveAttribute("href", "/operadores");
  });

  it("marca como atual o link da rota aberta", () => {
    renderWithProviders(<Sidebar />, { route: "/operadores", session: adminSession });

    expect(screen.getByRole("link", { name: "Operadores" })).toHaveAttribute("aria-current", "page");
    expect(screen.getByRole("link", { name: "Início" })).not.toHaveAttribute("aria-current");
  });

  it("não mostra os grupos que ficam sem itens para o perfil", () => {
    const { unmount } = renderWithProviders(<Sidebar />, { route: "/", session: adminSession });
    expect(screen.getByText("Operação")).toBeInTheDocument();
    expect(screen.getByText("Operadores", { selector: "p" })).toBeInTheDocument();
    expect(screen.queryByText("Conta")).not.toBeInTheDocument();
    unmount();

    renderWithProviders(<Sidebar />, { route: "/", session: operatorSession });
    expect(screen.getByText("Conta")).toBeInTheDocument();
    expect(screen.queryByText("Operadores", { selector: "p" })).not.toBeInTheDocument();
  });

  it("mostra a identificação do sistema e o aviso de acesso restrito", () => {
    renderWithProviders(<Sidebar />, { route: "/", session: adminSession });

    expect(screen.getByText("Controle de Acesso")).toBeInTheDocument();
    expect(screen.getByText("Console administrativo")).toBeInTheDocument();
    expect(screen.getByText("Acesso restrito a operadores autorizados.")).toBeInTheDocument();
  });
});
