import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Link, useLocation, useSearchParams } from "react-router-dom";
import { updateOperator } from "../api/operators";
import type { UpdateOperatorInput } from "../api/operators";
import { isAbortError, NO_ERROR, toFormError } from "../lib/form-error";
import type { FormError } from "../lib/form-error";
import { roleLabel } from "../lib/labels";
import { Alert } from "../components/ui/Alert";
import { Button } from "../components/ui/Button";
import { TextField } from "../components/ui/TextField";
import { PasswordField } from "../components/ui/PasswordField";
import { SelectField } from "../components/ui/SelectField";
import type { ApiNotification, Role, UpdatedOperator } from "../types/api";

interface EditContext {
  email?: string;
  role?: Role;
}

interface Baseline {
  email?: string;
  role?: Role;
}

function fieldError(notifications: ApiNotification[], field: string): string | undefined {
  return notifications.find((item) => item.key.toLowerCase() === field.toLowerCase())?.message;
}

export function EditOperatorPage() {
  const [searchParams] = useSearchParams();
  const location = useLocation();

  // O operador vem sempre da lista (?id=...). O identificador nao e exibido
  // nem editavel: segue apenas na URL da requisicao PUT.
  const operatorId = (searchParams.get("id") ?? "").trim();

  // A lista ja sabe o e-mail e o perfil atuais; eles chegam pelo estado de
  // navegacao para pre-preencher o formulario, sem precisar de outra API.
  const [baseline, setBaseline] = useState<Baseline>(() => {
    const context = (location.state as EditContext | null) ?? null;
    return { email: context?.email, role: context?.role };
  });

  const [email, setEmail] = useState(baseline.email ?? "");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState<Role>(baseline.role ?? "Operator");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<FormError>(NO_ERROR);
  const [updated, setUpdated] = useState<UpdatedOperator | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => () => abortRef.current?.abort(), []);

  function resetForm() {
    setEmail(baseline.email ?? "");
    setPassword("");
    setRole(baseline.role ?? "Operator");
    setError(NO_ERROR);
    setUpdated(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(NO_ERROR);
    setUpdated(null);

    // So envia o que realmente mudou em relacao ao que a lista informou.
    const payload: UpdateOperatorInput = {};
    const trimmedEmail = email.trim();
    if (trimmedEmail.length > 0 && trimmedEmail !== (baseline.email ?? "")) {
      payload.email = trimmedEmail;
    }
    if (password.length > 0) payload.password = password;
    if (baseline.role === undefined || role !== baseline.role) {
      payload.role = role;
    }

    setSubmitting(true);
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const result = await updateOperator(operatorId, payload, controller.signal);
      if (result.data) {
        setUpdated(result.data);
        setPassword("");
        setBaseline({ email: result.data.email, role: result.data.role });
      }
    } catch (err) {
      if (isAbortError(err)) return;
      setError(toFormError(err));
    } finally {
      setSubmitting(false);
    }
  }

  if (!operatorId) {
    return (
      <div className="page page--narrow">
        <header className="page-header">
          <h1 className="page-header__title">Editar operador</h1>
        </header>
        <section className="panel">
          <p className="empty-note">
            Escolha um operador na{" "}
            <Link to="/operadores" className="link">
              lista de operadores
            </Link>{" "}
            para editar.
          </p>
        </section>
      </div>
    );
  }

  return (
    <div className="page page--narrow">
      <header className="page-header">
        <h1 className="page-header__title">Editar operador</h1>
        <p className="page-header__description">
          Altere e-mail, senha ou perfil. Preencha somente os campos que deseja alterar.
        </p>
      </header>

      {updated ? (
        <section className="panel">
          <Alert tone="success" title="Conta atualizada com sucesso.">
            <dl className="desc-list desc-list--tight">
              <div className="desc-list__row">
                <dt>E-mail</dt>
                <dd>{updated.email}</dd>
              </div>
              <div className="desc-list__row">
                <dt>Perfil</dt>
                <dd>
                  <span className="tag">{roleLabel(updated.role)}</span>
                </dd>
              </div>
            </dl>
          </Alert>
          <div className="panel__actions">
            <Button type="button" onClick={resetForm}>
              Editar novamente
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
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              error={fieldError(error.notifications, "email")}
            />

            <PasswordField
              label="Nova senha"
              name="password"
              autoComplete="new-password"
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
              <Button type="submit" loading={submitting} loadingText="Salvando...">
                Salvar alteracoes
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
