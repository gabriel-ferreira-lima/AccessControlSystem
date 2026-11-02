import { screen, waitFor } from "@testing-library/react";
import { http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { API_BASE_URL } from "../lib/api-client";
import { toResponse } from "../test/envelope";
import { adminSession, buildNotification } from "../test/fixtures";
import { updateOperatorHandler } from "../test/handlers";
import { renderWithProviders } from "../test/render";
import { apiUrl, server } from "../test/server";
import type { Role } from "../types/api";
import { EditOperatorPage } from "./EditOperatorPage";

const OPERATOR_ID = "00000000-0000-4000-8000-0000000000aa";

interface RenderOptions {
  /** Busca da URL; o padrão traz o identificador. */
  search?: string;
  /** Estado de navegação vindo da lista (e-mail e perfil da linha). */
  state?: { email?: string; role?: Role };
}

function renderEdit({ search = `?id=${OPERATOR_ID}`, state }: RenderOptions = {}) {
  return renderWithProviders(<EditOperatorPage />, {
    route: `/operadores/editar${search}`,
    routeState: state,
    session: adminSession,
  });
}

const FROM_LIST = { email: "ana@example.test", role: "Admin" as Role };

function updated(overrides: Partial<{ email: string; role: Role }> = {}) {
  return { id: OPERATOR_ID, email: "ana@example.test", role: "Admin" as Role, ...overrides };
}

describe("EditOperatorPage: abertura e envio parcial", () => {
  it("preenche e-mail e perfil com o estado vindo da lista e deixa a senha vazia", () => {
    renderEdit({ state: FROM_LIST });

    expect(screen.getByRole("heading", { name: "Editar operador" })).toBeInTheDocument();
    expect(screen.getByLabelText("E-mail")).toHaveValue("ana@example.test");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Admin");
    expect(screen.getByLabelText("Nova senha")).toHaveValue("");
  });

  it("envia apenas o e-mail quando só o e-mail mudou", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: updated({ email: "novo@example.test" }) }, onRequest));
    const { user } = renderEdit({ state: FROM_LIST });

    await user.clear(screen.getByLabelText("E-mail"));
    await user.type(screen.getByLabelText("E-mail"), "novo@example.test");
    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest).toHaveBeenCalledTimes(1);
    expect(onRequest.mock.calls[0][0]).toMatchObject({
      method: "PUT",
      url: `${API_BASE_URL}/v1/account/update/${OPERATOR_ID}`,
      authorization: `Bearer ${adminSession.token}`,
      body: { email: "novo@example.test" },
    });
  });

  it("envia apenas a senha quando só a senha foi digitada", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: updated() }, onRequest));
    const { user } = renderEdit({ state: FROM_LIST });

    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");
    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest.mock.calls[0][0].body).toEqual({ password: "nova-senha-123" });
  });

  it("envia apenas o perfil quando só o perfil mudou", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: updated({ role: "Operator" }) }, onRequest));
    const { user } = renderEdit({ state: FROM_LIST });

    await user.selectOptions(screen.getByLabelText("Perfil de acesso"), "Operador");
    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest.mock.calls[0][0].body).toEqual({ role: "Operator" });
  });

  it("envia um corpo vazio quando nada mudou e deixa a api decidir", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: updated() }, onRequest));
    const { user } = renderEdit({ state: FROM_LIST });

    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest.mock.calls[0][0].body).toEqual({});
  });

  it("não envia o e-mail quando ele foi apagado", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: updated() }, onRequest));
    const { user } = renderEdit({ state: FROM_LIST });

    await user.clear(screen.getByLabelText("E-mail"));
    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest.mock.calls[0][0].body).toEqual({});
  });

  it("codifica o identificador na URL da requisição", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: updated() }, onRequest));
    const { user } = renderEdit({ search: "?id=ab%20cd", state: FROM_LIST });

    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest.mock.calls[0][0].url).toBe(`${API_BASE_URL}/v1/account/update/ab%20cd`);
  });

  it("abre em branco com perfil Operador quando acessada direto, sem o estado da lista", () => {
    renderEdit();

    expect(screen.getByLabelText("E-mail")).toHaveValue("");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Operator");
    expect(screen.getByLabelText("Nova senha")).toHaveValue("");
  });

  it("envia o perfil mesmo sem alteração quando acessada direto", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: updated({ role: "Operator" }) }, onRequest));
    const { user } = renderEdit();

    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");
    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest.mock.calls[0][0].body).toEqual({ password: "nova-senha-123", role: "Operator" });
  });

  it("envia e-mail e perfil quando acessada direto e o e-mail foi digitado", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: updated({ role: "Operator" }) }, onRequest));
    const { user } = renderEdit();

    await user.type(screen.getByLabelText("E-mail"), "novo@example.test");
    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest.mock.calls[0][0].body).toEqual({ email: "novo@example.test", role: "Operator" });
  });

  it.each([
    ["sem o parâmetro id", ""],
    ["com id só de espaços", "?id=%20%20"],
  ])("mostra a orientação de escolher um operador na lista quando aberta %s", (_descricao, search) => {
    renderEdit({ search });

    const orientation = screen.getByText(
      (_content, element) =>
        element?.tagName === "P" &&
        element.textContent === "Escolha um operador na lista de operadores para editar.",
    );
    expect(orientation).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "lista de operadores" })).toHaveAttribute(
      "href",
      "/operadores",
    );
    expect(screen.queryByLabelText("E-mail")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Salvar alterações" })).not.toBeInTheDocument();
  });
});

describe("EditOperatorPage: sucesso, erros e limpar", () => {
  it("mostra e-mail e perfil atualizados e usa-os como nova referência ao editar novamente", async () => {
    const onRequest = vi.fn();
    server.use(
      updateOperatorHandler(
        { kind: "success", data: updated({ email: "novo@example.test", role: "Operator" }) },
        onRequest,
      ),
    );
    const { user } = renderEdit({ state: FROM_LIST });
    await user.clear(screen.getByLabelText("E-mail"));
    await user.type(screen.getByLabelText("E-mail"), "novo@example.test");
    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");
    await user.selectOptions(screen.getByLabelText("Perfil de acesso"), "Operador");

    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    const success = await screen.findByRole("status");
    expect(success).toHaveTextContent("Conta atualizada com sucesso.");
    expect(success).toHaveTextContent("novo@example.test");
    expect(success).toHaveTextContent("Operador");
    expect(screen.queryByLabelText("E-mail")).not.toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Editar novamente" }));

    expect(screen.getByLabelText("E-mail")).toHaveValue("novo@example.test");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Operator");
    expect(screen.getByLabelText("Nova senha")).toHaveValue("");

    // A nova referência é a resposta da api: um segundo envio sem mudanças vai com corpo vazio.
    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));
    await screen.findByText("Conta atualizada com sucesso.");
    expect(onRequest).toHaveBeenCalledTimes(2);
    expect(onRequest.mock.calls[1][0].body).toEqual({});
  });

  it("mostra message e notifications, com erro por campo junto do campo, quando a api recusa", async () => {
    server.use(
      updateOperatorHandler({
        kind: "failure",
        status: 400,
        message: "Requisição inválida",
        notifications: [
          buildNotification("Email", "E-mail inválido"),
          buildNotification("PASSWORD", "Senha muito curta"),
          buildNotification("role", "Perfil inválido"),
          buildNotification("operador", "Regra sem campo"),
        ],
      }),
    );
    const { user } = renderEdit({ state: FROM_LIST });
    await user.type(screen.getByLabelText("Nova senha"), "123");

    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await screen.findByText("Requisição inválida");
    const alerts = screen.getAllByRole("alert");
    expect(alerts[0]).toHaveTextContent("Requisição inválida");
    expect(alerts[0]).toHaveTextContent("Regra sem campo");
    expect(screen.getByLabelText("E-mail")).toHaveAccessibleDescription("E-mail inválido");
    expect(screen.getByLabelText("Nova senha")).toHaveAccessibleDescription("Senha muito curta");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveAccessibleDescription("Perfil inválido");
  });

  it("volta e-mail e perfil ao valor da lista, esvazia a senha e some com o erro ao acionar Limpar", async () => {
    server.use(
      updateOperatorHandler({ kind: "failure", status: 409, message: "E-mail já cadastrado" }),
    );
    const { user } = renderEdit({ state: FROM_LIST });
    await user.clear(screen.getByLabelText("E-mail"));
    await user.type(screen.getByLabelText("E-mail"), "outro@example.test");
    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");
    await user.selectOptions(screen.getByLabelText("Perfil de acesso"), "Operador");
    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));
    await screen.findByText("E-mail já cadastrado");

    await user.click(screen.getByRole("button", { name: "Limpar" }));

    expect(screen.getByLabelText("E-mail")).toHaveValue("ana@example.test");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Admin");
    expect(screen.getByLabelText("Nova senha")).toHaveValue("");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("volta ao formulário em branco com perfil Operador ao acionar Limpar quando acessada direto", async () => {
    const { user } = renderEdit();
    await user.type(screen.getByLabelText("E-mail"), "outro@example.test");
    await user.selectOptions(screen.getByLabelText("Perfil de acesso"), "Administrador");

    await user.click(screen.getByRole("button", { name: "Limpar" }));

    expect(screen.getByLabelText("E-mail")).toHaveValue("");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Operator");
  });

  it("não mostra sucesso nem erro e mantém o formulário quando a api responde sucesso sem dados", async () => {
    const onRequest = vi.fn();
    server.use(updateOperatorHandler({ kind: "success", data: null }, onRequest));
    const { user } = renderEdit({ state: FROM_LIST });
    await user.type(screen.getByLabelText("Nova senha"), "nova-senha-123");

    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    await waitFor(() => expect(onRequest).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByRole("button", { name: "Salvar alterações" })).toBeEnabled());
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
      http.put(apiUrl("/v1/account/update/:id"), async () => {
        await gate;
        return toResponse({ kind: "success", data: updated() });
      }),
    );
    const { user } = renderEdit({ state: FROM_LIST });

    await user.click(screen.getByRole("button", { name: "Salvar alterações" }));

    expect(await screen.findByRole("button", { name: "Salvando..." })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Limpar" })).toBeDisabled();

    release();
    expect(await screen.findByText("Conta atualizada com sucesso.")).toBeInTheDocument();
  });
});
