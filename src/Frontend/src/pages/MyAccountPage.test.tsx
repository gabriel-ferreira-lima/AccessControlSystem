import { screen, waitFor } from "@testing-library/react";
import { http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { API_BASE_URL } from "../lib/api-client";
import { toResponse } from "../test/envelope";
import { buildNotification, operatorSession } from "../test/fixtures";
import { getMeHandler, updateMeHandler } from "../test/handlers";
import { renderWithProviders } from "../test/render";
import { apiUrl, server } from "../test/server";
import { MyAccountPage } from "./MyAccountPage";

const SERVER_EMAIL = "servidor@example.test";

function me(email = SERVER_EMAIL) {
  return { id: operatorSession.id, email, role: "Operator" as const };
}

function updatedMe(email = SERVER_EMAIL) {
  return { id: operatorSession.id, email };
}

function renderAccount() {
  return renderWithProviders(<MyAccountPage />, { route: "/minha-conta", session: operatorSession });
}

async function waitForLoaded() {
  await waitFor(() => expect(screen.getByLabelText("E-mail")).toHaveValue(SERVER_EMAIL));
}

describe("MyAccountPage: carga e envio", () => {
  it("carrega o get-me, preenche o e-mail e mostra o perfil como texto somente leitura", async () => {
    const onGetMe = vi.fn();
    server.use(getMeHandler({ kind: "success", data: me() }, onGetMe));
    renderAccount();

    expect(screen.getByRole("heading", { name: "Minha conta" })).toBeInTheDocument();
    expect(screen.getByText("—")).toBeInTheDocument();

    await waitForLoaded();

    expect(onGetMe).toHaveBeenCalledTimes(1);
    expect(onGetMe.mock.calls[0][0]).toMatchObject({
      method: "GET",
      url: `${API_BASE_URL}/v1/account/get-me`,
      authorization: `Bearer ${operatorSession.token}`,
    });
    expect(screen.getByLabelText("Nova senha")).toHaveValue("");
    expect(screen.getByText("Perfil de acesso")).toBeInTheDocument();
    expect(screen.getByText("Operador")).toBeInTheDocument();
    expect(screen.queryByRole("combobox")).not.toBeInTheDocument();
    expect(screen.queryByText("—")).not.toBeInTheDocument();
  });

  it("envia só o e-mail quando só o e-mail mudou", async () => {
    const onUpdate = vi.fn();
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({ kind: "success", data: updatedMe("novo@example.test") }, onUpdate),
    );
    const { user } = renderAccount();
    await waitForLoaded();

    await user.clear(screen.getByLabelText("E-mail"));
    await user.type(screen.getByLabelText("E-mail"), "novo@example.test");
    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    await waitFor(() => expect(onUpdate).toHaveBeenCalledTimes(1));
    expect(onUpdate.mock.calls[0][0]).toMatchObject({
      method: "PUT",
      url: `${API_BASE_URL}/v1/account/update-me`,
      authorization: `Bearer ${operatorSession.token}`,
      body: { email: "novo@example.test" },
    });
  });

  it("envia só a senha quando só a senha foi digitada", async () => {
    const onUpdate = vi.fn();
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({ kind: "success", data: updatedMe() }, onUpdate),
    );
    const { user } = renderAccount();
    await waitForLoaded();

    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");
    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    await waitFor(() => expect(onUpdate).toHaveBeenCalledTimes(1));
    expect(onUpdate.mock.calls[0][0].body).toEqual({ password: "nova-senha-123" });
  });

  it("envia e-mail e senha quando os dois mudaram", async () => {
    const onUpdate = vi.fn();
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({ kind: "success", data: updatedMe("novo@example.test") }, onUpdate),
    );
    const { user } = renderAccount();
    await waitForLoaded();

    await user.clear(screen.getByLabelText("E-mail"));
    await user.type(screen.getByLabelText("E-mail"), "novo@example.test");
    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");
    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    await waitFor(() => expect(onUpdate).toHaveBeenCalledTimes(1));
    expect(onUpdate.mock.calls[0][0].body).toEqual({
      email: "novo@example.test",
      password: "nova-senha-123",
    });
  });

  it("envia um corpo vazio quando nada mudou e deixa a api decidir", async () => {
    const onUpdate = vi.fn();
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({ kind: "success", data: updatedMe() }, onUpdate),
    );
    const { user } = renderAccount();
    await waitForLoaded();

    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    await waitFor(() => expect(onUpdate).toHaveBeenCalledTimes(1));
    expect(onUpdate.mock.calls[0][0].body).toEqual({});
  });

  it("ignora um e-mail que só tem espaços", async () => {
    const onUpdate = vi.fn();
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({ kind: "success", data: updatedMe() }, onUpdate),
    );
    const { user } = renderAccount();
    await waitForLoaded();

    await user.clear(screen.getByLabelText("E-mail"));
    await user.type(screen.getByLabelText("E-mail"), "   ");
    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    await waitFor(() => expect(onUpdate).toHaveBeenCalledTimes(1));
    expect(onUpdate.mock.calls[0][0].body).toEqual({});
  });

  it("mostra a mensagem da api, limpa a senha, recarrega os dados e atualiza o e-mail da sessão", async () => {
    let currentEmail = SERVER_EMAIL;
    let loads = 0;
    server.use(
      http.get(apiUrl("/v1/account/get-me"), () => {
        loads += 1;
        return toResponse({ kind: "success", data: me(currentEmail) });
      }),
      updateMeHandler(
        { kind: "success", message: "Dados atualizados com sucesso", data: updatedMe("novo@example.test") },
        () => {
          currentEmail = "novo@example.test";
        },
      ),
    );
    const { user } = renderAccount();
    await waitForLoaded();
    await user.clear(screen.getByLabelText("E-mail"));
    await user.type(screen.getByLabelText("E-mail"), "novo@example.test");
    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");

    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    expect(await screen.findByRole("status")).toHaveTextContent("Dados atualizados com sucesso");
    expect(screen.getByLabelText("Nova senha")).toHaveValue("");
    await waitFor(() => expect(loads).toBe(2));
    expect(screen.getByLabelText("E-mail")).toHaveValue("novo@example.test");
    const stored = JSON.parse(window.sessionStorage.getItem("acs.session") ?? "null");
    expect(stored).toMatchObject({ email: "novo@example.test", token: operatorSession.token });
  });

  it("não mostra sucesso nem erro e mantém a senha digitada quando a api responde sucesso sem dados", async () => {
    const onUpdate = vi.fn();
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({ kind: "success", data: null }, onUpdate),
    );
    const { user } = renderAccount();
    await waitForLoaded();
    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");

    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    await waitFor(() => expect(onUpdate).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByRole("button", { name: "Salvar alteracoes" })).toBeEnabled());
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Nova senha")).toHaveValue("nova-senha-123");
  });

  it("mostra o estado de carregamento enquanto envia", async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      http.put(apiUrl("/v1/account/update-me"), async () => {
        await gate;
        return toResponse({ kind: "success", data: updatedMe() });
      }),
    );
    const { user } = renderAccount();
    await waitForLoaded();

    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    expect(await screen.findByRole("button", { name: "Salvando..." })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Limpar" })).toBeDisabled();

    release();
    expect(await screen.findByRole("status")).toBeInTheDocument();
  });
});

describe("MyAccountPage: erros e limpar", () => {
  it("mostra message e notifications, com erro por campo junto do campo, quando o envio é recusado", async () => {
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({
        kind: "failure",
        status: 400,
        message: "Requisição inválida",
        notifications: [
          buildNotification("Email", "E-mail inválido"),
          buildNotification("password", "Senha muito curta"),
          buildNotification("operador", "Regra sem campo"),
        ],
      }),
    );
    const { user } = renderAccount();
    await waitForLoaded();
    await user.type(screen.getByLabelText("Nova senha"), "123");

    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));

    await screen.findByText("Requisição inválida");
    const alerts = screen.getAllByRole("alert");
    expect(alerts[0]).toHaveTextContent("Requisição inválida");
    expect(alerts[0]).toHaveTextContent("E-mail inválido");
    expect(alerts[0]).toHaveTextContent("Senha muito curta");
    expect(alerts[0]).toHaveTextContent("Regra sem campo");
    expect(screen.getByLabelText("E-mail")).toHaveAccessibleDescription("E-mail inválido");
    expect(screen.getByLabelText("Nova senha")).toHaveAccessibleDescription("Senha muito curta");
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("mostra o erro da carga acima do formulário, com o perfil ainda em branco", async () => {
    server.use(
      getMeHandler({
        kind: "failure",
        status: 400,
        message: "Requisição inválida",
        notifications: [buildNotification("Token", "Token sem identificador")],
      }),
    );
    renderAccount();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Requisição inválida");
    expect(alert).toHaveTextContent("Token sem identificador");
    expect(screen.getByLabelText("E-mail")).toHaveValue("");
    expect(screen.getByText("—")).toBeInTheDocument();
  });

  it("volta o e-mail ao atual, esvazia a senha e some com o erro ao acionar Limpar", async () => {
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({ kind: "failure", status: 409, message: "E-mail já cadastrado" }),
    );
    const { user } = renderAccount();
    await waitForLoaded();
    await user.clear(screen.getByLabelText("E-mail"));
    await user.type(screen.getByLabelText("E-mail"), "outro@example.test");
    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");
    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));
    await screen.findByText("E-mail já cadastrado");

    await user.click(screen.getByRole("button", { name: "Limpar" }));

    expect(screen.getByLabelText("E-mail")).toHaveValue(SERVER_EMAIL);
    expect(screen.getByLabelText("Nova senha")).toHaveValue("");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("some com a mensagem de sucesso ao acionar Limpar", async () => {
    server.use(
      getMeHandler({ kind: "success", data: me() }),
      updateMeHandler({ kind: "success", message: "Dados atualizados", data: updatedMe() }),
    );
    const { user } = renderAccount();
    await waitForLoaded();
    await user.click(screen.getByRole("button", { name: "Salvar alteracoes" }));
    await screen.findByText("Dados atualizados");

    await user.click(screen.getByRole("button", { name: "Limpar" }));

    expect(screen.queryByText("Dados atualizados")).not.toBeInTheDocument();
  });
});
