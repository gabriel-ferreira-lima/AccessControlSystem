import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { TextField } from "./TextField";

describe("TextField", () => {
  it("associa o rótulo ao campo", () => {
    render(<TextField label="E-mail" />);

    expect(screen.getByLabelText("E-mail")).toBeInTheDocument();
    expect(screen.getByRole("textbox", { name: "E-mail" })).toBeInTheDocument();
  });

  it("não mostra alerta nem marca o campo como inválido quando não há erro", () => {
    render(<TextField label="E-mail" />);

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.getByLabelText("E-mail")).not.toHaveAttribute("aria-invalid");
    expect(screen.getByLabelText("E-mail")).not.toHaveAccessibleDescription();
  });

  it("mostra o erro em um alerta, marca o campo como inválido e o descreve", () => {
    render(<TextField label="E-mail" error="E-mail inválido" />);

    expect(screen.getByRole("alert")).toHaveTextContent("E-mail inválido");
    const field = screen.getByLabelText("E-mail");
    expect(field).toHaveAttribute("aria-invalid", "true");
    expect(field).toHaveAccessibleDescription("E-mail inválido");
  });

  it("repassa atributos ao campo e avisa as mudanças digitadas", async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    render(<TextField label="E-mail" type="email" placeholder="nome@empresa" onChange={onChange} />);

    const field = screen.getByLabelText("E-mail");
    await user.type(field, "ana");

    expect(field).toHaveAttribute("type", "email");
    expect(field).toHaveAttribute("placeholder", "nome@empresa");
    expect(onChange).toHaveBeenCalledTimes(3);
    expect(field).toHaveValue("ana");
  });
});
