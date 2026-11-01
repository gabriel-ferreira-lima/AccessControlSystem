import { screen, waitFor } from "@testing-library/react";
import { http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { API_BASE_URL } from "../lib/api-client";
import { toResponse } from "../test/envelope";
import { adminSession, buildNotification } from "../test/fixtures";
import { createOperatorHandler } from "../test/handlers";
import { renderWithProviders } from "../test/render";
import { apiUrl, server } from "../test/server";
import { CreateOperatorPage } from "./CreateOperatorPage";

const CREATED = {
  id: "00000000-0000-4000-8000-0000000000c1",
  email: "nova@example.test",
  role: "Admin",
} as const;

function renderCreate() {
  return renderWithProviders(<CreateOperatorPage />, {
    route: "/operadores/novo",
    session: adminSession,
  });
}

async function fillForm(user: ReturnType<typeof renderCreate>["user"]) {
  await user.type(screen.getByLabelText("E-mail"), "nova@example.test");
  await user.type(screen.getByLabelText("Senha"), "senha-secreta");
  await user.selectOptions(screen.getByLabelText("Perfil de acesso"), "Administrador");
}

describe("CreateOperatorPage", () => {
  it("mostra o formulário em branco com o perfil Operador", () => {
    renderCreate();

    expect(screen.getByRole("heading", { name: "Criar operador" })).toBeInTheDocument();
    expect(screen.getByLabelText("E-mail")).toHaveValue("");
    expect(screen.getByLabelText("Senha")).toHaveValue("");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Operator");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("envia e-mail, senha e perfil e mostra identificador, e-mail e perfil do operador criado", async () => {
    const onRequest = vi.fn();
    server.use(
      createOperatorHandler(
        { kind: "success", status: 201, message: "Operador criado", data: CREATED },
        onRequest,
      ),
    );
    const { user } = renderCreate();

    await fillForm(user);
    await user.click(screen.getByRole("button", { name: "Criar operador" }));

    const success = await screen.findByRole("status");
    expect(success).toHaveTextContent("Operador criado com sucesso.");
    expect(success).toHaveTextContent(CREATED.id);
    expect(success).toHaveTextContent("nova@example.test");
    expect(success).toHaveTextContent("Administrador");
    expect(onRequest).toHaveBeenCalledTimes(1);
    expect(onRequest.mock.calls[0][0]).toMatchObject({
      method: "POST",
      url: `${API_BASE_URL}/v1/account/create`,
      authorization: `Bearer ${adminSession.token}`,
      body: { email: "nova@example.test", password: "senha-secreta", role: "Admin" },
    });
    expect(screen.queryByLabelText("E-mail")).not.toBeInTheDocument();
  });

  it("volta em branco, com o perfil Operador, ao cadastrar outro", async () => {
    server.use(createOperatorHandler({ kind: "success", status: 201, data: CREATED }));
    const { user } = renderCreate();
    await fillForm(user);
    await user.click(screen.getByRole("button", { name: "Criar operador" }));

    await user.click(await screen.findByRole("button", { name: "Cadastrar outro" }));

    expect(screen.getByLabelText("E-mail")).toHaveValue("");
    expect(screen.getByLabelText("Senha")).toHaveValue("");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Operator");
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
  });

  it("não mostra sucesso nem erro e mantém os dados quando a api responde sucesso sem dados", async () => {
    const onRequest = vi.fn();
    server.use(createOperatorHandler({ kind: "success", status: 201, data: null }, onRequest));
    const { user } = renderCreate();
    await fillForm(user);

    await user.click(screen.getByRole("button", { name: "Criar operador" }));

    await waitFor(() => expect(onRequest).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(screen.getByRole("button", { name: "Criar operador" })).toBeEnabled());
    expect(screen.queryByRole("status")).not.toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
    expect(screen.getByLabelText("E-mail")).toHaveValue("nova@example.test");
  });

  it("mostra o estado de carregamento e bloqueia Limpar enquanto envia", async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    server.use(
      http.post(apiUrl("/v1/account/create"), async () => {
        await gate;
        return toResponse({ kind: "success", status: 201, data: CREATED });
      }),
    );
    const { user } = renderCreate();
    await fillForm(user);

    await user.click(screen.getByRole("button", { name: "Criar operador" }));

    expect(await screen.findByRole("button", { name: "Criando..." })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Limpar" })).toBeDisabled();

    release();
    expect(await screen.findByText("Operador criado com sucesso.")).toBeInTheDocument();
  });

  it("mostra message, notifications e erro junto do campo e preserva os dados quando a api recusa", async () => {
    server.use(
      createOperatorHandler({
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
    const { user } = renderCreate();
    await fillForm(user);

    await user.click(screen.getByRole("button", { name: "Criar operador" }));

    await screen.findByText("Requisição inválida");
    // O alerta de cima traz tudo, como a api devolveu.
    const alerts = screen.getAllByRole("alert");
    expect(alerts[0]).toHaveTextContent("Requisição inválida");
    expect(alerts[0]).toHaveTextContent("E-mail inválido");
    expect(alerts[0]).toHaveTextContent("Senha muito curta");
    expect(alerts[0]).toHaveTextContent("Regra sem campo");
    // E-mail e senha também aparecem junto do campo (a chave é comparada sem diferenciar maiúsculas).
    expect(screen.getByLabelText("E-mail")).toHaveAccessibleDescription("E-mail inválido");
    expect(screen.getByLabelText("Senha")).toHaveAccessibleDescription("Senha muito curta");
    expect(screen.getByLabelText("Perfil de acesso")).not.toHaveAttribute("aria-invalid");
    // Os dados digitados continuam no formulário.
    expect(screen.getByLabelText("E-mail")).toHaveValue("nova@example.test");
    expect(screen.getByLabelText("Senha")).toHaveValue("senha-secreta");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Admin");
  });

  it("mostra o erro do perfil junto do seletor", async () => {
    server.use(
      createOperatorHandler({
        kind: "failure",
        status: 400,
        message: "Requisição inválida",
        notifications: [buildNotification("Role", "Perfil inválido")],
      }),
    );
    const { user } = renderCreate();
    await fillForm(user);

    await user.click(screen.getByRole("button", { name: "Criar operador" }));

    await screen.findByText("Requisição inválida");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveAccessibleDescription("Perfil inválido");
  });

  it("esvazia os campos, volta o perfil para Operador e some com o erro ao acionar Limpar", async () => {
    server.use(
      createOperatorHandler({
        kind: "failure",
        status: 409,
        message: "E-mail já cadastrado",
      }),
    );
    const { user } = renderCreate();
    await fillForm(user);
    await user.click(screen.getByRole("button", { name: "Criar operador" }));
    await screen.findByText("E-mail já cadastrado");

    await user.click(screen.getByRole("button", { name: "Limpar" }));

    expect(screen.getByLabelText("E-mail")).toHaveValue("");
    expect(screen.getByLabelText("Senha")).toHaveValue("");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Operator");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
