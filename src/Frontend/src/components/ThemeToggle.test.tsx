import { screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { renderWithProviders } from "../test/render";
import { ThemeToggle } from "./ThemeToggle";

describe("ThemeToggle", () => {
  it("começa no tema claro (preferência do sistema) oferecendo o tema escuro", () => {
    renderWithProviders(<ThemeToggle />);

    expect(screen.getByRole("button", { name: "Usar tema escuro" })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute("data-theme", "light");
  });

  it("alterna para o tema escuro, aplica no documento e guarda a escolha", async () => {
    const { user } = renderWithProviders(<ThemeToggle />);

    await user.click(screen.getByRole("button", { name: "Usar tema escuro" }));

    expect(screen.getByRole("button", { name: "Usar tema claro" })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute("data-theme", "dark");
    expect(window.localStorage.getItem("acs.theme")).toBe("dark");
  });

  it("volta ao tema claro ao acionar de novo e guarda a escolha", async () => {
    const { user } = renderWithProviders(<ThemeToggle />);
    await user.click(screen.getByRole("button", { name: "Usar tema escuro" }));

    await user.click(screen.getByRole("button", { name: "Usar tema claro" }));

    expect(screen.getByRole("button", { name: "Usar tema escuro" })).toBeInTheDocument();
    expect(document.documentElement).toHaveAttribute("data-theme", "light");
    expect(window.localStorage.getItem("acs.theme")).toBe("light");
  });
});
