import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderWithProviders } from "../test/render";
import { HomePage } from "./HomePage";

describe("HomePage", () => {
  it("mostra o título, a descrição e o aviso de que as informações serão adicionadas", () => {
    renderWithProviders(<HomePage />, { route: "/" });

    expect(screen.getByRole("heading", { name: "Inicio" })).toBeInTheDocument();
    expect(screen.getByText("Console de controle de acesso.")).toBeInTheDocument();
    expect(
      screen.getByText(
        "As informacoes desta pagina serao adicionadas conforme os servicos forem disponibilizados.",
      ),
    ).toBeInTheDocument();
  });
});
