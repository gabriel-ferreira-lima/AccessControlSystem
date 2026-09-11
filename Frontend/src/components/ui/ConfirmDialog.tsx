import { useEffect, useId, useRef } from "react";
import type { ReactNode } from "react";
import { Button } from "./Button";
import { Alert } from "./Alert";
import type { FormError } from "../../lib/form-error";

interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description?: ReactNode;
  confirmLabel: string;
  cancelLabel?: string;
  tone?: "default" | "danger";
  pending?: boolean;
  error?: FormError | null;
  onConfirm: () => void;
  onCancel: () => void;
}

/**
 * Janela de confirmacao generica ("tem certeza?"). Usa o elemento nativo
 * <dialog> — foco preso e tecla Esc ja vem de graca do navegador.
 */
export function ConfirmDialog({
  open,
  title,
  description,
  confirmLabel,
  cancelLabel = "Cancelar",
  tone = "default",
  pending = false,
  error,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const titleId = useId();

  useEffect(() => {
    const dialog = dialogRef.current;
    if (!dialog) return;

    if (open && !dialog.open) {
      dialog.showModal();
    } else if (!open && dialog.open) {
      dialog.close();
    }
  }, [open]);

  return (
    <dialog
      ref={dialogRef}
      className="confirm-dialog"
      aria-labelledby={titleId}
      onCancel={(event) => {
        // Esc: mantem a decisao com o componente pai em vez do fechamento nativo.
        event.preventDefault();
        if (!pending) onCancel();
      }}
    >
      <h2 className="confirm-dialog__title" id={titleId}>
        {title}
      </h2>
      {description ? <div className="confirm-dialog__description">{description}</div> : null}
      {error ? (
        <Alert tone="error" title={error.message} notifications={error.notifications} />
      ) : null}
      <div className="confirm-dialog__actions">
        <Button type="button" variant="ghost" onClick={onCancel} disabled={pending}>
          {cancelLabel}
        </Button>
        <Button
          type="button"
          variant={tone === "danger" ? "danger" : "primary"}
          onClick={onConfirm}
          loading={pending}
          loadingText="Aguarde..."
        >
          {confirmLabel}
        </Button>
      </div>
    </dialog>
  );
}
