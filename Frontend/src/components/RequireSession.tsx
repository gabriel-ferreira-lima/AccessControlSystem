import { Navigate, useLocation } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "../context/AuthContext";

/**
 * Redireciona para o login quando nao ha sessao local.
 * E apenas conveniencia de navegacao: o controle de acesso efetivo
 * e feito pela API, que valida o token em cada requisicao.
 */
export function RequireSession({ children }: { children: ReactNode }) {
  const { session } = useAuth();
  const location = useLocation();

  if (!session) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  }

  return <>{children}</>;
}
