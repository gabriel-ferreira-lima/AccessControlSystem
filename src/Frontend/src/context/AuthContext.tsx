import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import type { ReactNode } from "react";
import type { Session } from "../types/api";
import { clearSession, readSession, writeSession } from "../lib/session-storage";
import { setUnauthorizedHandler } from "../lib/api-client";

interface AuthContextValue {
  session: Session | null;
  /** Verdadeiro quando a sessao foi encerrada pelo servidor (token nao aceito), nao por logout manual. */
  sessionExpired: boolean;
  /** Verdadeiro quando a sessao acabou porque o operador trocou a propria senha (o token deixou de valer na hora). */
  passwordChanged: boolean;
  signIn: (session: Session) => void;
  signOut: () => void;
  /** Encerra a sessao depois de enviar uma senha nova: o login mostra "Senha alterada. Faca login novamente.". */
  signOutAfterPasswordChange: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => readSession());
  const [sessionExpired, setSessionExpired] = useState(false);
  const [passwordChanged, setPasswordChanged] = useState(false);

  const signIn = useCallback((next: Session) => {
    writeSession(next);
    setSession(next);
    setSessionExpired(false);
    setPasswordChanged(false);
  }, []);

  const signOut = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);

  // Depois de enviar uma senha nova com sucesso, o token antigo ja nao vale (a Api o recusa na hora) e ela nao
  // devolve token novo: encerra a sessao local e avisa no login. Nao e "sessao encerrada pelo servidor" (401).
  const signOutAfterPasswordChange = useCallback(() => {
    clearSession();
    setSession(null);
    setSessionExpired(false);
    setPasswordChanged(true);
  }, []);

  // Chamado pelo api-client quando qualquer requisicao autenticada volta 401:
  // o servidor decidiu que o token nao vale mais (conta desativada, expirado
  // etc.). So encerra a sessao local — RequireSession cuida do redirecionamento
  // assim que "session" vira null.
  const expireSession = useCallback(() => {
    clearSession();
    setSession(null);
    setSessionExpired(true);
    setPasswordChanged(false);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(expireSession);
    return () => setUnauthorizedHandler(null);
  }, [expireSession]);

  const value = useMemo<AuthContextValue>(
    () => ({ session, sessionExpired, passwordChanged, signIn, signOut, signOutAfterPasswordChange }),
    [session, sessionExpired, passwordChanged, signIn, signOut, signOutAfterPasswordChange],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth deve ser usado dentro de <AuthProvider>.");
  }
  return context;
}
