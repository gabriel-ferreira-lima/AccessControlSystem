import { useEffect, useRef, useState } from "react";
import type { FormEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { login } from "../api/auth";
import { isAbortError, toFormError, UNEXPECTED_ERROR } from "../lib/form-error";
import type { FormError } from "../lib/form-error";
import { useAuth } from "../context/AuthContext";
import { Alert } from "../components/ui/Alert";
import { Button } from "../components/ui/Button";
import { TextField } from "../components/ui/TextField";
import { ThemeToggle } from "../components/ThemeToggle";
import { IconShield } from "../components/icons";

interface LocationState {
  from?: string;
}

export function LoginPage() {
  const { session, sessionExpired, signIn } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const destination = (location.state as LocationState | null)?.from ?? "/";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<FormError | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  useEffect(() => () => abortRef.current?.abort(), []);

  if (session) {
    return <Navigate to={destination} replace />;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);

    abortRef.current?.abort();
    const controller = new AbortController();
    abortRef.current = controller;

    try {
      const result = await login({ email, password }, controller.signal);
      if (!result.data?.token) {
        setError(UNEXPECTED_ERROR);
        return;
      }
      signIn(result.data);
      navigate(destination, { replace: true });
    } catch (err) {
      if (isAbortError(err)) return;
      setError(toFormError(err));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="auth">
      <div className="auth__toolbar">
        <ThemeToggle />
      </div>

      <main className="auth__panel">
        <div className="auth__brand">
          <IconShield className="auth__brand-mark" />
          <div>
            <h1 className="auth__title">Sistema de Controle de Acesso</h1>
            <p className="auth__subtitle">Console administrativo &mdash; acesso restrito</p>
          </div>
        </div>

        <form className="auth__form" onSubmit={handleSubmit} noValidate>
          {error ? (
            <Alert tone="error" title={error.message} notifications={error.notifications} />
          ) : sessionExpired ? (
            <Alert tone="info" title="Sua sessao foi encerrada. Faca login novamente." />
          ) : null}

          <TextField
            label="E-mail"
            type="email"
            name="email"
            autoComplete="username"
            autoCapitalize="none"
            autoCorrect="off"
            spellCheck={false}
            autoFocus
            required
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />

          <TextField
            label="Senha"
            type="password"
            name="password"
            autoComplete="current-password"
            required
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />

          <Button
            type="submit"
            className="auth__submit"
            loading={submitting}
            loadingText="Autenticando..."
          >
            Entrar
          </Button>
        </form>

        <p className="auth__notice">
          O uso deste sistema e limitado a operadores autorizados.
        </p>
      </main>
    </div>
  );
}
