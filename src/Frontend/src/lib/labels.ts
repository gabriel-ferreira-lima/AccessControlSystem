import type { Role } from "../types/api";

/** Rotulo de exibicao para o perfil de acesso (apenas apresentacao). */
export function roleLabel(role: Role): string {
  switch (role) {
    case "Admin":
      return "Administrador";
    case "Operator":
      return "Operador";
    default:
      return role;
  }
}
