import { useId } from "react";
import type { SelectHTMLAttributes } from "react";

interface SelectFieldProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, "id"> {
  label: string;
  error?: string | null;
}

export function SelectField({ label, error, className, children, ...rest }: SelectFieldProps) {
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
      <div className="field__select">
        <select
          id={id}
          className="field__control"
          aria-invalid={error ? true : undefined}
          aria-describedby={errorId}
          {...rest}
        >
          {children}
        </select>
        <span className="field__select-caret" aria-hidden="true" />
      </div>
      {error ? (
        <p className="field__error" id={errorId} role="alert">
          {error}
        </p>
      ) : null}
    </div>
  );
}
