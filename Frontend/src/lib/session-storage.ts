import type { Session } from "../types/api";

// A sessao vive somente em sessionStorage: e descartada ao fechar a aba.
// O token e a unica credencial; o backend o valida a cada requisicao.
const STORAGE_KEY = "acs.session";

export function readSession(): Session | null {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (!raw) return null;

    const parsed = JSON.parse(raw) as Partial<Session>;
    if (!parsed || typeof parsed.token !== "string" || parsed.token.length === 0) {
      return null;
    }

    return {
      token: parsed.token,
      id: typeof parsed.id === "string" ? parsed.id : "",
      email: typeof parsed.email === "string" ? parsed.email : "",
      role: parsed.role === "Admin" ? "Admin" : "Operator",
    };
  } catch {
    return null;
  }
}

export function writeSession(session: Session): void {
  try {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  } catch {
    /* armazenamento indisponivel: a sessao permanece apenas em memoria */
  }
}

export function clearSession(): void {
  try {
    window.sessionStorage.removeItem(STORAGE_KEY);
  } catch {
    /* nada a fazer */
  }
}
