import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { PasswordField } from "./PasswordField";

describe("PasswordField", () => {
  it("associa o rótulo ao campo, que começa oculto", () => {
    render(<PasswordField label="Senha" />);

    expect(screen.getByLabelText("Senha")).toHaveAttribute("type", "password");
    expect(screen.getByRole("button", { name: "Mostrar senha" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  it("mostra e oculta a senha digitada ao acionar o botão", async () => {
    const user = userEvent.setup();
    render(<PasswordField label="Senha" />);
    const field = screen.getByLabelText("Senha");
    await user.type(field, "segredo");

    await user.click(screen.getByRole("button", { name: "Mostrar senha" }));

    expect(field).toHaveAttribute("type", "text");
    expect(field).toHaveValue("segredo");
    expect(screen.getByRole("button", { name: "Ocultar senha" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );

    await user.click(screen.getByRole("button", { name: "Ocultar senha" }));

    expect(field).toHaveAttribute("type", "password");
    expect(screen.getByRole("button", { name: "Mostrar senha" })).toBeInTheDocument();
  });

  it("o botão de mostrar não envia o formulário", async () => {
    const user = userEvent.setup();
    let submitted = false;
    render(
      <form
        onSubmit={(event) => {
          event.preventDefault();
          submitted = true;
        }}
      >
        <PasswordField label="Senha" />
      </form>,
    );

    await user.click(screen.getByRole("button", { name: "Mostrar senha" }));

    expect(submitted).toBe(false);
  });

  it("mostra o erro em um alerta, marca o campo como inválido e o descreve", () => {
    render(<PasswordField label="Senha" error="Senha muito curta" />);

    expect(screen.getByRole("alert")).toHaveTextContent("Senha muito curta");
    const field = screen.getByLabelText("Senha");
    expect(field).toHaveAttribute("aria-invalid", "true");
    expect(field).toHaveAccessibleDescription("Senha muito curta");
  });

  it("não mostra alerta quando não há erro", () => {
    render(<PasswordField label="Senha" />);

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Senha")).not.toHaveAttribute("aria-invalid");
  });
});
