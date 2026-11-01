import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderWithProviders } from "../test/render";
import { NotFoundPage } from "./NotFoundPage";

describe("NotFoundPage", () => {
  it("mostra o título, a descrição e o link de volta ao início", () => {
    renderWithProviders(<NotFoundPage />, { route: "/endereco-inexistente" });

    expect(screen.getByRole("heading", { name: "Pagina nao encontrada" })).toBeInTheDocument();
    expect(screen.getByText("O recurso solicitado nao existe ou foi movido.")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Voltar ao painel" })).toHaveAttribute("href", "/");
  });

  it("leva ao início ao acionar o link de volta", async () => {
    const { user } = renderWithProviders(<NotFoundPage />, { route: "/endereco-inexistente" });

    await user.click(screen.getByRole("link", { name: "Voltar ao painel" }));

    expect(await screen.findByText("Rota atual: /")).toBeInTheDocument();
  });
});
