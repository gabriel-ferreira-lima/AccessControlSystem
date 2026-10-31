import { useId, useState } from "react";
import type { InputHTMLAttributes } from "react";
import { IconEye, IconEyeOff } from "../icons";

interface PasswordFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id" | "type"> {
  label: string;
  error?: string | null;
}

export function PasswordField({ label, error, className, ...rest }: PasswordFieldProps) {
  const id = useId();
  const [revealed, setRevealed] = useState(false);
  const errorId = error ? `${id}-error` : undefined;

  const wrapperClass = ["field", error ? "field--invalid" : "", className ?? ""]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={wrapperClass}>
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      <div className="field__password">
        <input
          id={id}
          type={revealed ? "text" : "password"}
          className="field__control"
          aria-invalid={error ? true : undefined}
          aria-describedby={errorId}
          {...rest}
        />
        <button
          type="button"
          className="field__reveal"
          onClick={() => setRevealed((current) => !current)}
          aria-pressed={revealed}
          aria-label={revealed ? "Ocultar senha" : "Mostrar senha"}
          title={revealed ? "Ocultar senha" : "Mostrar senha"}
        >
          {revealed ? <IconEyeOff /> : <IconEye />}
        </button>
      </div>
      {error ? (
        <p className="field__error" id={errorId} role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}
