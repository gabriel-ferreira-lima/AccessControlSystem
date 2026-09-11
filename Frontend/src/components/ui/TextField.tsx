import { useId } from "react";
import type { InputHTMLAttributes } from "react";

interface TextFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, "id"> {
  label: string;
  error?: string | null;
}

export function TextField({ label, error, className, ...rest }: TextFieldProps) {
  const id = useId();
  const errorId = error ? `${id}-error` : undefined;

  const wrapperClass = ["field", error ? "field--invalid" : "", className ?? ""]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={wrapperClass}>
      <label className="field__label" htmlFor={id}>
        {label}
      </label>
      <input
        id={id}
        className="field__control"
        aria-invalid={error ? true : undefined}
        aria-describedby={errorId}
        {...rest}
      />
      {error ? (
        <p className="field__error" id={errorId} role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}
