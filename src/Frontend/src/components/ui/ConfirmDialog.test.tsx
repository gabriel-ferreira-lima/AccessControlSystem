import { fireEvent, render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { ComponentProps } from "react";
import { describe, expect, it, vi } from "vitest";
import { buildNotification } from "../../test/fixtures";
import { ConfirmDialog } from "./ConfirmDialog";

function renderDialog(props: Partial<ComponentProps<typeof ConfirmDialog>> = {}) {
  const onConfirm = vi.fn();
  const onCancel = vi.fn();
  const result = render(
    <ConfirmDialog
      open
      title="Desativar operador?"
      description={<>Tem certeza que deseja desativar ana@example.test?</>}
      confirmLabel="Desativar"
      onConfirm={onConfirm}
      onCancel={onCancel}
      {...props}
    />,
  );
  return { ...result, onConfirm, onCancel, user: userEvent.setup() };
}

/** Dispara o evento que o navegador envia ao <dialog> quando se aperta Esc. */
function pressEscape(dialog: HTMLElement): boolean {
  return fireEvent(dialog, new Event("cancel", { cancelable: true }));
}

describe("ConfirmDialog", () => {
  it("não fica acessível enquanto está fechado", () => {
    renderDialog({ open: false });

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("mostra título, descrição e os botões quando está aberto", () => {
    renderDialog();

    const dialog = screen.getByRole("dialog", { name: "Desativar operador?" });
    expect(dialog).toHaveTextContent("Tem certeza que deseja desativar ana@example.test?");
    expect(within(dialog).getByRole("button", { name: "Cancelar" })).toBeEnabled();
    expect(within(dialog).getByRole("button", { name: "Desativar" })).toBeEnabled();
  });

  it("usa o rótulo de cancelar informado", () => {
    renderDialog({ cancelLabel: "Voltar" });

    expect(screen.getByRole("button", { name: "Voltar" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Cancelar" })).not.toBeInTheDocument();
  });

  it("chama onConfirm ao confirmar e onCancel ao cancelar", async () => {
    const { user, onConfirm, onCancel } = renderDialog();

    await user.click(screen.getByRole("button", { name: "Desativar" }));
    expect(onConfirm).toHaveBeenCalledTimes(1);
    expect(onCancel).not.toHaveBeenCalled();

    await user.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onCancel).toHaveBeenCalledTimes(1);
  });

  it.each(["default", "danger"] as const)("confirma normalmente com o tom %s", async (tone) => {
    const { user, onConfirm } = renderDialog({ tone });

    await user.click(screen.getByRole("button", { name: "Desativar" }));

    expect(onConfirm).toHaveBeenCalledTimes(1);
  });

  it("entrega a decisão ao componente pai ao apertar Esc, sem fechar sozinho", () => {
    const { onCancel } = renderDialog();
    const dialog = screen.getByRole("dialog");

    const notPrevented = pressEscape(dialog);

    expect(notPrevented).toBe(false);
    expect(onCancel).toHaveBeenCalledTimes(1);
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("bloqueia confirmar, cancelar e Esc enquanto a chamada está em andamento", async () => {
    const { user, onConfirm, onCancel } = renderDialog({ pending: true });
    const dialog = screen.getByRole("dialog");

    const confirm = within(dialog).getByRole("button", { name: "Aguarde..." });
    const cancel = within(dialog).getByRole("button", { name: "Cancelar" });
    expect(confirm).toBeDisabled();
    expect(cancel).toBeDisabled();

    await user.click(confirm);
    await user.click(cancel);
    const notPrevented = pressEscape(dialog);

    expect(notPrevented).toBe(false);
    expect(onConfirm).not.toHaveBeenCalled();
    expect(onCancel).not.toHaveBeenCalled();
  });

  it("mostra a message e as notifications do erro dentro da janela", () => {
    renderDialog({
      error: {
        message: "Requisição inválida",
        notifications: [buildNotification("Id", "Não é possível desativar a própria conta")],
      },
    });

    const alert = within(screen.getByRole("dialog")).getByRole("alert");
    expect(alert).toHaveTextContent("Requisição inválida");
    expect(alert).toHaveTextContent("Não é possível desativar a própria conta");
  });

  it("não mostra alerta quando não há erro", () => {
    renderDialog({ error: null });

    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("fecha quando open volta a ser falso e reabre quando volta a ser verdadeiro", () => {
    const { rerender, onConfirm, onCancel } = renderDialog();
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    const closed = (open: boolean) => (
      <ConfirmDialog
        open={open}
        title="Desativar operador?"
        confirmLabel="Desativar"
        onConfirm={onConfirm}
        onCancel={onCancel}
      />
    );
    rerender(closed(false));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();

    rerender(closed(true));
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });
});
