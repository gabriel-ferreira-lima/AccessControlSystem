import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http } from "msw";
import { afterEach, describe, expect, it, vi } from "vitest";
import { apiRequest } from "../lib/api-client";
import { toResponse } from "../test/envelope";
import { buildSession } from "../test/fixtures";
import { storeSession } from "../test/render";
import { apiUrl, server } from "../test/server";
import { AuthProvider, useAuth } from "./AuthContext";

// Componente sonda: expõe o estado do contexto em texto e os comandos em botões.
function Probe() {
  const { session, sessionExpired, signIn, signOut } = useAuth();
  return (
    <div>
      <p>sessão: {session ? session.email : "nenhuma"}</p>
      <p>expirada: {String(sessionExpired)}</p>
      <button type="button" onClick={() => signIn(buildSession({ email: "novo@example.test" }))}>
        entrar
      </button>
      <button type="button" onClick={signOut}>
        sair
      </button>
      <button
        type="button"
        onClick={() => void apiRequest("/v1/qualquer", { authenticated: true }).catch(() => undefined)}
      >
        chamar
      </button>
    </div>
  );
}

function renderProbe() {
  render(
    <AuthProvider>
      <Probe />
    </AuthProvider>,
  );
  return userEvent.setup();
}

function respondWith401() {
  server.use(
    http.get(apiUrl("/v1/qualquer"), () =>
      toResponse({ kind: "failure", status: 401, message: "Não autorizado" }),
    ),
  );
}

describe("AuthProvider", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("começa sem sessão quando não há nada guardado", () => {
    renderProbe();

    expect(screen.getByText("sessão: nenhuma")).toBeInTheDocument();
    expect(screen.getByText("expirada: false")).toBeInTheDocument();
  });

  it("começa com a sessão guardada no sessionStorage", () => {
    storeSession(buildSession({ email: "guardada@example.test" }));

    renderProbe();

    expect(screen.getByText("sessão: guardada@example.test")).toBeInTheDocument();
  });

  it("signIn guarda a sessão no sessionStorage e atualiza o estado", async () => {
    const user = renderProbe();

    await user.click(screen.getByRole("button", { name: "entrar" }));

    expect(screen.getByText("sessão: novo@example.test")).toBeInTheDocument();
    expect(window.sessionStorage.getItem("acs.session")).toContain("novo@example.test");
  });

  it("signOut apaga a sessão sem marcar a sessão como expirada", async () => {
    storeSession(buildSession({ email: "guardada@example.test" }));
    const user = renderProbe();

    await user.click(screen.getByRole("button", { name: "sair" }));

    expect(screen.getByText("sessão: nenhuma")).toBeInTheDocument();
    expect(screen.getByText("expirada: false")).toBeInTheDocument();
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
  });

  it("apaga a sessão e marca como expirada quando uma chamada autenticada recebe 401", async () => {
    respondWith401();
    storeSession(buildSession({ email: "guardada@example.test" }));
    const user = renderProbe();

    await user.click(screen.getByRole("button", { name: "chamar" }));

    await waitFor(() => expect(screen.getByText("expirada: true")).toBeInTheDocument());
    expect(screen.getByText("sessão: nenhuma")).toBeInTheDocument();
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
  });

  it("signIn depois de uma sessão expirada zera o aviso de expiração", async () => {
    respondWith401();
    storeSession(buildSession());
    const user = renderProbe();
    await user.click(screen.getByRole("button", { name: "chamar" }));
    await waitFor(() => expect(screen.getByText("expirada: true")).toBeInTheDocument());

    await user.click(screen.getByRole("button", { name: "entrar" }));

    expect(screen.getByText("expirada: false")).toBeInTheDocument();
    expect(screen.getByText("sessão: novo@example.test")).toBeInTheDocument();
  });

  it("deixa de reagir ao 401 depois de desmontado", async () => {
    respondWith401();
    storeSession(buildSession());

    const { unmount } = render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );
    unmount();

    await expect(apiRequest("/v1/qualquer", { authenticated: true })).rejects.toMatchObject({
      status: 401,
    });
    // O handler foi desligado no desmonte: a sessão guardada não foi apagada.
    expect(window.sessionStorage.getItem("acs.session")).not.toBeNull();
  });

  it("mantém a sessão em memória quando o armazenamento da aba está indisponível", async () => {
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("armazenamento indisponível");
    });
    const user = renderProbe();

    await user.click(screen.getByRole("button", { name: "entrar" }));

    expect(screen.getByText("sessão: novo@example.test")).toBeInTheDocument();
  });
});

describe("useAuth", () => {
  it("lança um erro claro quando usado fora do AuthProvider", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);

    expect(() => render(<Probe />)).toThrow("useAuth deve ser usado dentro de <AuthProvider>.");

    vi.restoreAllMocks();
  });
});
