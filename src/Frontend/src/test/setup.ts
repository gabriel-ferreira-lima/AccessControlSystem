import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterAll, afterEach, beforeAll } from "vitest";
import { recordUnhandledRequest, server, takeUnhandledRequests } from "./server";

// Servidor MSW: qualquer chamada sem handler é registrada e reprova o teste no afterEach.
// Com `onUnhandledRequest` em forma de função, o MSW deixa a requisição seguir para a REDE REAL
// se a função não lançar (e `print.error()` também não a impede, medido neste ambiente). Lançar um
// Error próprio faz o MSW responder uma resposta 500 sintética, sem sair da máquina.
beforeAll(() => {
  server.listen({
    onUnhandledRequest(request) {
      recordUnhandledRequest(request);
      throw new Error(`Chamada de API não prevista no teste: ${request.method} ${request.url}`);
    },
  });
});

afterEach(() => {
  server.resetHandlers();
  cleanup();
  window.sessionStorage.clear();
  window.localStorage.clear();

  const unhandled = takeUnhandledRequests();
  if (unhandled.length > 0) {
    throw new Error(`Chamada de API não prevista no teste: ${unhandled.join(", ")}`);
  }
});

afterAll(() => {
  server.close();
});

// --- Lacunas do jsdom em relação ao navegador (ver research.md, item 3) ---

// O jsdom não implementa showModal/close do <dialog>; o ConfirmDialog depende deles.
HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
  this.setAttribute("open", "");
};
HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
  this.removeAttribute("open");
  this.dispatchEvent(new Event("close"));
};

// O jsdom não implementa matchMedia; o ThemeProvider usa. Configurável para um teste poder
// trocá-lo (ex.: preferência escura) e restaurar este ao final.
Object.defineProperty(window, "matchMedia", {
  configurable: true,
  writable: true,
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: () => {},
    removeEventListener: () => {},
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  }),
});

// O fetch do Node rejeita um abort com o DOMException do Node, mas o jsdom troca o DOMException
// global por outro. Sem alinhar, isAbortError (instanceof DOMException) falha e uma chamada
// abortada viraria "erro de rede". No navegador existe um só DOMException.
const NodeDOMException = AbortSignal.abort().reason.constructor;
Object.defineProperty(globalThis, "DOMException", {
  configurable: true,
  writable: true,
  value: NodeDOMException,
});
