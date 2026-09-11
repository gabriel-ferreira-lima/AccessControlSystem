import { Navigate } from "react-router-dom";
import type { ReactNode } from "react";
import { useAuth } from "../context/AuthContext";
import type { Role } from "../types/api";

interface RequireRoleProps {
  role: Role;
  children: ReactNode;
}

/**
 * Oculta uma rota quando o perfil da sessao nao corresponde ao exigido.
 * E apenas conveniencia de navegacao: a API valida o perfil do token e
 * responde 403 independentemente do que o cliente exibe.
 */
export function RequireRole({ role, children }: RequireRoleProps) {
  const { session } = useAuth();

  if (session?.role !== role) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}
