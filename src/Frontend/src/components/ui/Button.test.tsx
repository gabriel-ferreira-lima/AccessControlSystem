import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import { Button } from "./Button";

describe("Button", () => {
  it("é do tipo button por padrão, para não enviar formulários sem querer", async () => {
    const user = userEvent.setup();
    let submitted = false;
    render(
      <form
        onSubmit={(event) => {
          event.preventDefault();
          submitted = true;
        }}
      >
        <Button>Salvar</Button>
      </form>,
    );

    expect(screen.getByRole("button", { name: "Salvar" })).toHaveAttribute("type", "button");
    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(submitted).toBe(false);
  });

  it("envia o formulário quando é do tipo submit", async () => {
    const user = userEvent.setup();
    let submitted = false;
    render(
      <form
        onSubmit={(event) => {
          event.preventDefault();
          submitted = true;
        }}
      >
        <Button type="submit">Salvar</Button>
      </form>,
    );

    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(submitted).toBe(true);
  });

  it("chama onClick ao clicar", async () => {
    const onClick = vi.fn();
    const user = userEvent.setup();
    render(<Button onClick={onClick}>Salvar</Button>);

    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it("não chama onClick quando está desabilitado", async () => {
    const onClick = vi.fn();
    const user = userEvent.setup();
    render(
      <Button onClick={onClick} disabled>
        Salvar
      </Button>,
    );

    await user.click(screen.getByRole("button", { name: "Salvar" }));

    expect(screen.getByRole("button", { name: "Salvar" })).toBeDisabled();
    expect(onClick).not.toHaveBeenCalled();
  });

  it("mostra o texto de carregamento, fica ocupado e desabilitado quando está carregando", async () => {
    const onClick = vi.fn();
    const user = userEvent.setup();
    render(
      <Button loading loadingText="Salvando..." onClick={onClick}>
        Salvar
      </Button>,
    );

    const button = screen.getByRole("button", { name: "Salvando..." });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute("aria-busy", "true");
    expect(screen.queryByText("Salvar")).not.toBeInTheDocument();

    await user.click(button);
    expect(onClick).not.toHaveBeenCalled();
  });

  it("mantém o texto normal quando está carregando e não há texto de carregamento", () => {
    render(<Button loading>Salvar</Button>);

    expect(screen.getByRole("button", { name: "Salvar" })).toBeDisabled();
  });

  it("não está ocupado quando não está carregando", () => {
    render(<Button>Salvar</Button>);

    expect(screen.getByRole("button", { name: "Salvar" })).not.toHaveAttribute("aria-busy");
  });

  it("esconde o ícone dos leitores de tela: o nome acessível é só o texto", () => {
    render(<Button icon={<span>ícone decorativo</span>}>Salvar</Button>);

    expect(screen.getByRole("button", { name: "Salvar" })).toBeInTheDocument();
  });

  it.each(["primary", "ghost", "danger"] as const)(
    "funciona do mesmo jeito na variante %s",
    async (variant) => {
      const onClick = vi.fn();
      const user = userEvent.setup();
      render(
        <Button variant={variant} size="sm" onClick={onClick}>
          Salvar
        </Button>,
      );

      await user.click(screen.getByRole("button", { name: "Salvar" }));

      expect(onClick).toHaveBeenCalledTimes(1);
    },
  );
});
