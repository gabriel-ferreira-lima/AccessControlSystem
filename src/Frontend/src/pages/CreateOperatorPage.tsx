import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { createOperator } from "../api/operators";
import { isAbortError, NO_ERROR, toFormError } from "../lib/form-error";
import type { FormError } from "../lib/form-error";
import { roleLabel } from "../lib/labels";
import { Alert } from "../components/ui/Alert";
import { Button } from "../components/ui/Button";
import { TextField } from "../components/ui/TextField";
import { PasswordField } from "../components/ui/PasswordField";
import { SelectField } from "../components/ui/SelectField";
import type { ApiNotification, CreatedOperator, Role } from "../types/api";

function fieldError(notifications: ApiNotification[], field: string): string | undefined {
  return notifications.find((item) => item.key.toLowerCase() === field.toLowerCase())?.message;
}

export function CreateOperatorPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<Role>("Operator");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<FormError>(NO_ERROR);
  const [created, setCreated] = useState<CreatedOperator | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => () => abortRef.current?.abort(), []);

  function resetForm() {
    setEmail("");
    setPassword("");
    setRole("Operator");
    setError(NO_ERROR);
    setCreated(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(NO_ERROR);
    setCreated(null);
    setSubmitting(true);

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const result = await createOperator({ email, password, role }, controller.signal);
      if (result.data) {
        setCreated(result.data);
        setEmail("");
        setPassword("");
        setRole("Operator");
      }
    } catch (err) {
      if (isAbortError(err)) return;
      setError(toFormError(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page page--narrow">
      <header className="page-header">
        <h1 className="page-header__title">Criar operador</h1>
        <p className="page-header__description">
          Cadastre um novo operador e defina o respectivo perfil de acesso.
        </p>
      </header>

      {created ? (
        <section className="panel">
          <Alert tone="success" title="Operador criado com sucesso.">
            <dl className="desc-list desc-list--tight">
              <div className="desc-list__row">
                <dt>Identificador</dt>
                <dd className="mono">{created.id}</dd>
              </div>
              <div className="desc-list__row">
                <dt>E-mail</dt>
                <dd>{created.email}</dd>
              </div>
              <div className="desc-list__row">
                <dt>Perfil</dt>
                <dd>
                  <span className="tag">{roleLabel(created.role)}</span>
                </dd>
              </div>
            </dl>
          </Alert>
          <div className="panel__actions">
            <Button type="button" onClick={resetForm}>
              Cadastrar outro
            </Button>
          </div>
        </section>
      ) : (
        <section className="panel">
          <form className="form" onSubmit={handleSubmit} noValidate>
            {error.message ? (
              <Alert tone="error" title={error.message} notifications={error.notifications} />
            ) : null}

            <TextField
              label="E-mail"
              type="email"
              name="email"
              autoComplete="off"
              autoCapitalize="none"
              autoCorrect="off"
              spellCheck={false}
              required
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              error={fieldError(error.notifications, "email")}
            />

            <PasswordField
              label="Senha"
              name="password"
              autoComplete="new-password"
              required
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              error={fieldError(error.notifications, "password")}
            />

            <SelectField
              label="Perfil de acesso"
              name="role"
              value={role}
              onChange={(event) => setRole(event.target.value as Role)}
              error={fieldError(error.notifications, "role")}
            >
              <option value="Operator">Operador</option>
              <option value="Admin">Administrador</option>
            </SelectField>

            <div className="form__actions">
              <Button type="submit" loading={submitting} loadingText="Criando...">
                Criar operador
              </Button>
              <Button type="button" variant="ghost" onClick={resetForm} disabled={submitting}>
                Limpar
              </Button>
            </div>
          </form>
        </section>
      )}
    </div>
  );
}
