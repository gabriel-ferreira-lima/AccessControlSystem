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
  signIn: (session: Session) => void;
  signOut: () => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(() => readSession());
  const [sessionExpired, setSessionExpired] = useState(false);

  const signIn = useCallback((next: Session) => {
    writeSession(next);
    setSession(next);
    setSessionExpired(false);
  }, []);

  const signOut = useCallback(() => {
    clearSession();
    setSession(null);
  }, []);

  // Chamado pelo api-client quando qualquer requisicao autenticada volta 401:
  // o servidor decidiu que o token nao vale mais (conta desativada, expirado
  // etc.). So encerra a sessao local — RequireSession cuida do redirecionamento
  // assim que "session" vira null.
  const expireSession = useCallback(() => {
    clearSession();
    setSession(null);
    setSessionExpired(true);
  }, []);

  useEffect(() => {
    setUnauthorizedHandler(expireSession);
    return () => setUnauthorizedHandler(null);
  }, [expireSession]);

  const value = useMemo<AuthContextValue>(
    () => ({ session, sessionExpired, signIn, signOut }),
    [session, sessionExpired, signIn, signOut],
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
