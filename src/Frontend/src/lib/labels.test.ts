import { describe, expect, it } from "vitest";
import type { Role } from "../types/api";
import { roleLabel } from "./labels";

describe("roleLabel", () => {
  it("devolve Administrador para o perfil Admin", () => {
    expect(roleLabel("Admin")).toBe("Administrador");
  });

  it("devolve Operador para o perfil Operator", () => {
    expect(roleLabel("Operator")).toBe("Operador");
  });

  it("devolve o próprio valor para um perfil desconhecido", () => {
    expect(roleLabel("Auditor" as Role)).toBe("Auditor");
  });
});
