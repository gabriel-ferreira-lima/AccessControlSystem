import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderWithProviders } from "../test/render";
import { HomePage } from "./HomePage";

describe("HomePage", () => {
  it("mostra o título, a descrição e o aviso de que as informações serão adicionadas", () => {
    renderWithProviders(<HomePage />, { route: "/" });

    expect(screen.getByRole("heading", { name: "Início" })).toBeInTheDocument();
    expect(screen.getByText("Console de controle de acesso.")).toBeInTheDocument();
    expect(
      screen.getByText(
        "As informações desta página serão adicionadas conforme os serviços forem disponibilizados.",
      ),
    ).toBeInTheDocument();
  });
});
