import { setupServer } from "msw/node";
import { API_BASE_URL } from "../lib/api-client";

/** Servidor MSW compartilhado pelos testes; cada teste declara seus handlers com `server.use(...)`. */
export const server = setupServer();

// Chamadas que chegaram ao MSW sem handler ("MÉTODO URL"). O setup.ts reprova o teste
// que deixar alguma aqui, porque o MSW sozinho só imprime o erro e rejeita a requisição.
const unhandledRequests: string[] = [];

/** Monta a URL como o api-client: base da API (sem barra final) + caminho. */
export function apiUrl(path: string): string {
  return `${API_BASE_URL}${path}`;
}

/** Usada pelo setup.ts em `onUnhandledRequest`. */
export function recordUnhandledRequest(request: Request): void {
  unhandledRequests.push(`${request.method} ${request.url}`);
}

/** Devolve e esvazia as chamadas sem handler registradas até agora. */
export function takeUnhandledRequests(): string[] {
  return unhandledRequests.splice(0);
}
