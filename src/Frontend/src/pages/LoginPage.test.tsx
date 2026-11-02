import { screen, waitFor } from "@testing-library/react";
import { http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { loginHandler } from "../test/handlers";
import { adminSession, buildNotification, buildSession } from "../test/fixtures";
import { toResponse } from "../test/envelope";
import { renderWithProviders } from "../test/render";
import { apiUrl, server } from "../test/server";
import { LoginPage } from "./LoginPage";

const EXPIRED_NOTICE = "Sua sessão foi encerrada. Faça login novamente.";

function renderLogin(options: Parameters<typeof renderWithProviders>[1] = {}) {
  return renderWithProviders(<LoginPage />, { route: "/login", ...options });
}

async function fillAndSubmit(user: ReturnType<typeof renderLogin>["user"]) {
  await user.type(screen.getByLabelText("E-mail"), "ana@example.test");
  await user.type(screen.getByLabelText("Senha"), "senha-secreta");
  await user.click(screen.getByRole("button", { name: "Entrar" }));
}

describe("LoginPage", () => {
  it("guarda a sessão e leva ao início quando o login tem sucesso", async () => {
    const onRequest = vi.fn();
    const session = buildSession({ role: "Admin", email: "ana@example.test" });
    server.use(loginHandler({ kind: "success", data: session }, onRequest));
    const { user } = renderLogin();

    await fillAndSubmit(user);

    expect(await screen.findByText("Rota atual: /")).toBeInTheDocument();
    expect(onRequest).toHaveBeenCalledTimes(1);
    expect(onRequest.mock.calls[0][0]).toMatchObject({
      method: "POST",
      authorization: null,
      body: { email: "ana@example.test", password: "senha-secreta" },
    });
    expect(JSON.parse(window.sessionStorage.getItem("acs.session") ?? "null")).toEqual(session);
  });

  it("leva à rota de origem depois do login", async () => {
    server.use(loginHandler({ kind: "success", data: buildSession({ role: "Admin" }) }));
    const { user } = renderLogin({ routeState: { from: "/operadores" } });

    await fillAndSubmit(user);

    expect(await screen.findByText("Rota atual: /operadores")).toBeInTheDocument();
  });

  it("mostra a message e as notifications quando a api devolve 401", async () => {
    server.use(
      loginHandler({
        kind: "failure",
        status: 401,
        message: "Credenciais inválidas",
        notifications: [
          buildNotification("Email", "E-mail ou senha incorretos"),
          buildNotification("Password", "Tente novamente"),
        ],
      }),
    );
    const { user } = renderLogin();

    await fillAndSubmit(user);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Credenciais inválidas");
    expect(alert).toHaveTextContent("E-mail ou senha incorretos");
    expect(alert).toHaveTextContent("Tente novamente");
    expect(screen.getByRole("heading", { name: "Sistema de Controle de Acesso" })).toBeInTheDocument();
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
    expect(screen.queryByText(EXPIRED_NOTICE)).not.toBeInTheDocument();
  });

  it("mostra erro inesperado e não cria sessão quando a resposta vem sem token", async () => {
    server.use(loginHandler({ kind: "success", data: buildSession({ token: "" }) }));
    const { user } = renderLogin();

    await fillAndSubmit(user);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Ocorreu um erro inesperado. Tente novamente.",
    );
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
    expect(screen.queryByText(/Rota atual/)).not.toBeInTheDocument();
  });

  it("mostra erro inesperado e não cria sessão quando o sucesso vem sem dados", async () => {
    server.use(loginHandler({ kind: "success", data: null }));
    const { user } = renderLogin();

    await fillAndSubmit(user);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Ocorreu um erro inesperado. Tente novamente.",
    );
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
  });

  it("redireciona para o início quando já há sessão", async () => {
    renderLogin({ session: adminSession });

    expect(await screen.findByText("Rota atual: /")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Entrar" })).not.toBeInTheDocument();
  });

  it("redireciona para a rota de origem quando já há sessão e há origem", async () => {
    renderLogin({ session: adminSession, routeState: { from: "/operadores" } });

    expect(await screen.findByText("Rota atual: /operadores")).toBeInTheDocument();
  });

  it("mostra o estado de carregamento e bloqueia o botão enquanto envia", async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.post(apiUrl("/v1/account/login"), async () => {
        await gate;
        return toResponse({ kind: "success", data: buildSession({ role: "Admin" }) });
      }),
    );
    const { user } = renderLogin();

    await fillAndSubmit(user);

    const loading = await screen.findByRole("button", { name: "Autenticando..." });
    expect(loading).toBeDisabled();
    expect(screen.queryByRole("button", { name: "Entrar" })).not.toBeInTheDocument();

    release();
    expect(await screen.findByText("Rota atual: /")).toBeInTheDocument();
  });

  it("mostra o formulário em branco com os campos de e-mail e senha", () => {
    renderLogin();

    expect(screen.getByLabelText("E-mail")).toHaveValue("");
    expect(screen.getByLabelText("Senha")).toHaveValue("");
    expect(screen.getByRole("button", { name: "Entrar" })).toBeEnabled();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.queryByText(EXPIRED_NOTICE)).not.toBeInTheDocument();
    expect(screen.getByText("O uso deste sistema é limitado a operadores autorizados.")).toBeInTheDocument();
  });
});

describe("LoginPage: falhas de comunicação", () => {
  it("mostra a mensagem de falha de comunicação quando o servidor está inacessível", async () => {
    server.use(loginHandler({ kind: "network-error" }));
    const { user } = renderLogin();

    await fillAndSubmit(user);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente.",
    );
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
  });

  it("mostra a mensagem genérica de erro inesperado quando a resposta de erro vem fora do envelope", async () => {
    server.use(loginHandler({ kind: "raw", status: 502, body: "<html>Bad Gateway</html>" }));
    const { user } = renderLogin();

    await fillAndSubmit(user);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Ocorreu um erro inesperado. Tente novamente em instantes.",
    );
  });

  it("mostra a message e as notifications do envelope quando o HTTP é de sucesso mas o envelope marca falha", async () => {
    server.use(
      loginHandler({
        kind: "failure-with-ok-status",
        message: "Credenciais inválidas",
        notifications: [buildNotification("Email", "E-mail ou senha incorretos")],
      }),
    );
    const { user } = renderLogin();

    await fillAndSubmit(user);

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Credenciais inválidas");
    expect(alert).toHaveTextContent("E-mail ou senha incorretos");
  });

  it("não derruba sessão nem mostra o aviso de sessão encerrada quando o login recebe 401", async () => {
    server.use(loginHandler({ kind: "failure", status: 401, message: "Credenciais inválidas" }));
    const { user } = renderLogin();

    await fillAndSubmit(user);

    expect(await screen.findByRole("alert")).toHaveTextContent("Credenciais inválidas");
    expect(screen.queryByText(EXPIRED_NOTICE)).not.toBeInTheDocument();
  });

  it("aborta a chamada e não mostra erro quando a tela sai de vista com o envio em andamento", async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    let aborted = false;
    server.use(
      http.post(apiUrl("/v1/account/login"), async ({ request }) => {
        request.signal.addEventListener("abort", () => {
          aborted = true;
        });
        await gate;
        return toResponse({ kind: "success", data: buildSession({ role: "Admin" }) });
      }),
    );
    const { user, unmount } = renderLogin();
    await fillAndSubmit(user);
    await screen.findByRole("button", { name: "Autenticando..." });

    unmount();

    await waitFor(() => expect(aborted).toBe(true));
    release();
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
  });
});
