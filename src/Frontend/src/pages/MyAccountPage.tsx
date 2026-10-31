import { useCallback, useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { getMe, updateMe } from "../api/me";
import { isAbortError, NO_ERROR, toFormError } from "../lib/form-error";
import type { FormError } from "../lib/form-error";
import { roleLabel } from "../lib/labels";
import { useAuth } from "../context/AuthContext";
import { Alert } from "../components/ui/Alert";
import { Button } from "../components/ui/Button";
import { TextField } from "../components/ui/TextField";
import { PasswordField } from "../components/ui/PasswordField";
import type { ApiNotification, Me } from "../types/api";

function fieldError(notifications: ApiNotification[], field: string): string | undefined {
  return notifications.find((item) => item.key.toLowerCase() === field.toLowerCase())?.message;
}

export function MyAccountPage() {
  const { session, signIn } = useAuth();

  // Dados atuais, vindos direto do servidor (GET /account/get-me).
  // O perfil e somente leitura: o operador nao pode alterar o proprio perfil.
  const [me, setMe] = useState<Me | null>(null);
  const [meError, setMeError] = useState<FormError | null>(null);

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<FormError>(NO_ERROR);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const loadMe = useCallback((signal?: AbortSignal) => {
    setMeError(null);

    return getMe(signal)
      .then((result) => {
        if (result.data) {
          setMe(result.data);
          // Pre-preenche com o e-mail atual (a senha nunca vem do servidor).
          setEmail(result.data.email);
        }
      })
      .catch((err: unknown) => {
        if (isAbortError(err)) return;
        setMeError(toFormError(err));
      });
  }, []);

  useEffect(() => {
    const controller = new AbortController();
    loadMe(controller.signal);
    return () => controller.abort();
  }, [loadMe]);

  useEffect(() => () => abortRef.current?.abort(), []);

  function resetForm() {
    setEmail(me?.email ?? "");
    setPassword("");
    setError(NO_ERROR);
    setSuccessMessage(null);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(NO_ERROR);
    setSuccessMessage(null);

    // O e-mail vem pre-preenchido com o atual; so entra no payload se foi
    // de fato alterado. A senha so entra se algo foi digitado.
    const payload: { email?: string; password?: string } = {};
    const trimmedEmail = email.trim();
    if (trimmedEmail.length > 0 && trimmedEmail !== me?.email) {
      payload.email = trimmedEmail;
    }
    if (password.length > 0) payload.password = password;

    setSubmitting(true);
    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const result = await updateMe(payload, controller.signal);
      if (result.data) {
        if (session) {
          signIn({ ...session, email: result.data.email });
        }
        setSuccessMessage(result.message);
        setPassword("");
        // Recarrega do servidor; o e-mail exibido/pre-preenchido vem de la.
        loadMe();
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
        <h1 className="page-header__title">Minha conta</h1>
        <p className="page-header__description">
          Altere seu e-mail ou sua senha. Preencha somente o que deseja alterar.
        </p>
      </header>

      <section className="panel">
        <form className="form" onSubmit={handleSubmit} noValidate>
          {meError ? (
            <Alert tone="error" title={meError.message} notifications={meError.notifications} />
          ) : null}
          {successMessage ? <Alert tone="success" title={successMessage} /> : null}
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

          <div className="field">
            <span className="field__label">Perfil de acesso</span>
            <p className="field__static">
              {me ? <span className="tag">{roleLabel(me.role)}</span> : "—"}
            </p>
          </div>

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
    </div>
  );
}
