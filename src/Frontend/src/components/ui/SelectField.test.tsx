import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { SelectField } from "./SelectField";

function renderSelect(props: { error?: string; onChange?: () => void } = {}) {
  return render(
    <SelectField label="Perfil de acesso" defaultValue="Operator" {...props}>
      <option value="Operator">Operador</option>
      <option value="Admin">Administrador</option>
    </SelectField>,
  );
}

describe("SelectField", () => {
  it("associa o rótulo ao seletor e renderiza as opções filhas", () => {
    renderSelect();

    const select = screen.getByRole("combobox", { name: "Perfil de acesso" });
    expect(select).toHaveValue("Operator");
    expect(screen.getAllByRole("option").map((option) => option.textContent)).toEqual([
      "Operador",
      "Administrador",
    ]);
  });

  it("avisa a mudança com o valor escolhido", async () => {
    const onChange = vi.fn();
    const user = userEvent.setup();
    renderSelect({ onChange });

    await user.selectOptions(screen.getByRole("combobox", { name: "Perfil de acesso" }), "Administrador");

    expect(onChange).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("combobox", { name: "Perfil de acesso" })).toHaveValue("Admin");
  });

  it("mostra o erro em um alerta, marca o seletor como inválido e o descreve", () => {
    renderSelect({ error: "Perfil inválido" });

    expect(screen.getByRole("alert")).toHaveTextContent("Perfil inválido");
    const select = screen.getByRole("combobox", { name: "Perfil de acesso" });
    expect(select).toHaveAttribute("aria-invalid", "true");
    expect(select).toHaveAccessibleDescription("Perfil inválido");
  });

  it("não mostra alerta quando não há erro", () => {
    renderSelect();

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.getByRole("combobox", { name: "Perfil de acesso" })).not.toHaveAttribute(
      "aria-invalid",
    );
  });
});
