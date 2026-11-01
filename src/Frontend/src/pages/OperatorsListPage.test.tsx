import { fireEvent, screen, waitFor, within } from "@testing-library/react";
import { http } from "msw";
import { describe, expect, it, vi } from "vitest";
import { toResponse } from "../test/envelope";
import {
  adminSession,
  buildNotification,
  buildOperator,
  buildOperatorList,
} from "../test/fixtures";
import {
  activateOperatorHandler,
  deactivateOperatorHandler,
  listOperatorsHandler,
} from "../test/handlers";
import type { ListOperatorsQuery } from "../test/handlers";
import { renderWithProviders } from "../test/render";
import { API_BASE_URL } from "../lib/api-client";
import { apiUrl, server } from "../test/server";
import type { OperatorListItem } from "../types/api";
import { OperatorsListPage } from "./OperatorsListPage";

function renderList() {
  return renderWithProviders(<OperatorsListPage />, { route: "/operadores", session: adminSession });
}

/** Responde com `total` operadores sintéticos, fatiados conforme a página e o tamanho pedidos. */
function pagedList(total: number, queries: ListOperatorsQuery[]) {
  return listOperatorsHandler((query) => {
    queries.push(query);
    const first = (query.page - 1) * query.size;
    const count = Math.max(0, Math.min(query.size, total - first));
    const operators = Array.from({ length: count }, (_, index) =>
      buildOperator({ email: `p${query.page}-${index + 1}@example.test` }),
    );
    return {
      kind: "success",
      data: buildOperatorList(operators, { page: query.page, size: query.size, total }),
    };
  });
}

/** Responde à n-ésima carga com a n-ésima lista (a última se repete). */
function sequencedLists(lists: OperatorListItem[][], onLoad = vi.fn()) {
  let call = 0;
  const handler = listOperatorsHandler(() => {
    const operators = lists[Math.min(call, lists.length - 1)];
    call += 1;
    return { kind: "success", data: buildOperatorList(operators) };
  }, onLoad);
  return { handler, onLoad };
}

/** Permite segurar uma resposta do MSW até o teste liberá-la. */
function createGate() {
  let release: () => void = () => undefined;
  const promise = new Promise<void>((resolve) => {
    release = resolve;
  });
  return { promise, release: () => release() };
}

describe("OperatorsListPage: carga, filtro, paginação e erros", () => {
  it("carrega a primeira página com o filtro Ativos", async () => {
    const queries: ListOperatorsQuery[] = [];
    server.use(
      listOperatorsHandler((query) => {
        queries.push(query);
        return {
          kind: "success",
          data: buildOperatorList([
            buildOperator({ email: "ana@example.test", role: "Admin" }),
            buildOperator({ email: "bia@example.test", role: "Operator" }),
          ]),
        };
      }),
    );
    renderList();

    expect(screen.getByText("Carregando operadores...")).toBeInTheDocument();
    expect(await screen.findByText("ana@example.test")).toBeInTheDocument();

    expect(queries).toEqual([{ page: 1, size: 10, isActive: "Active" }]);
    expect(screen.getByRole("combobox", { name: "Mostrar" })).toHaveValue("Active");
    const anaRow = screen.getByRole("row", { name: /ana@example.test/ });
    expect(within(anaRow).getByText("Administrador")).toBeInTheDocument();
    expect(within(anaRow).getByText("Ativo")).toBeInTheDocument();
    const biaRow = screen.getByRole("row", { name: /bia@example.test/ });
    expect(within(biaRow).getByText("Operador")).toBeInTheDocument();
    expect(screen.getByText(/2 operador\(es\) · pagina 1 de 1/)).toBeInTheDocument();
  });

  it("mostra a situação Inativo para operadores inativos", async () => {
    server.use(
      listOperatorsHandler({
        kind: "success",
        data: buildOperatorList([buildOperator({ email: "caio@example.test", isActive: false })]),
      }),
    );
    renderList();

    const row = await screen.findByRole("row", { name: /caio@example.test/ });

    expect(within(row).getByText("Inativo")).toBeInTheDocument();
  });

  it("pede Inativos e Todos e volta à página 1 ao trocar o filtro", async () => {
    const queries: ListOperatorsQuery[] = [];
    server.use(pagedList(25, queries));
    const { user } = renderList();
    await screen.findByText("p1-1@example.test");
    await user.click(screen.getByRole("button", { name: "Proxima" }));
    await screen.findByText(/pagina 2 de 3/);
    await waitFor(() => expect(screen.getByRole("combobox", { name: "Mostrar" })).toBeEnabled());

    await user.selectOptions(screen.getByRole("combobox", { name: "Mostrar" }), "Inativos");

    await waitFor(() =>
      expect(queries.at(-1)).toEqual({ page: 1, size: 10, isActive: "Inactive" }),
    );
    await screen.findByText(/pagina 1 de 3/);
    await waitFor(() => expect(screen.getByRole("combobox", { name: "Mostrar" })).toBeEnabled());

    await user.selectOptions(screen.getByRole("combobox", { name: "Mostrar" }), "Todos");

    await waitFor(() => expect(queries.at(-1)).toEqual({ page: 1, size: 10, isActive: "All" }));
  });

  it("navega entre páginas e desabilita Anterior na primeira e Próxima na última", async () => {
    const queries: ListOperatorsQuery[] = [];
    server.use(pagedList(25, queries));
    const { user } = renderList();
    await screen.findByText(/25 operador\(es\) · pagina 1 de 3/);
    await waitFor(() => expect(screen.getByRole("button", { name: "Proxima" })).toBeEnabled());
    expect(screen.getByRole("button", { name: "Anterior" })).toBeDisabled();

    await user.click(screen.getByRole("button", { name: "Proxima" }));
    await screen.findByText(/pagina 2 de 3/);
    await waitFor(() => expect(screen.getByRole("button", { name: "Proxima" })).toBeEnabled());
    expect(screen.getByRole("button", { name: "Anterior" })).toBeEnabled();

    await user.click(screen.getByRole("button", { name: "Proxima" }));
    await screen.findByText(/pagina 3 de 3/);
    await waitFor(() => expect(screen.getByRole("button", { name: "Anterior" })).toBeEnabled());
    expect(screen.getByRole("button", { name: "Proxima" })).toBeDisabled();
    expect(screen.getByText("p3-1@example.test")).toBeInTheDocument();

    await user.click(screen.getByRole("button", { name: "Anterior" }));
    await screen.findByText(/pagina 2 de 3/);

    expect(queries.map((query) => query.page)).toEqual([1, 2, 3, 2]);
  });

  it("pede o novo tamanho e volta à página 1 ao mudar os itens por página", async () => {
    const queries: ListOperatorsQuery[] = [];
    server.use(pagedList(25, queries));
    const { user } = renderList();
    await screen.findByText("p1-1@example.test");
    await user.click(screen.getByRole("button", { name: "Proxima" }));
    await screen.findByText(/pagina 2 de 3/);
    await waitFor(() =>
      expect(screen.getByRole("combobox", { name: "Itens por pagina" })).toBeEnabled(),
    );

    await user.selectOptions(screen.getByRole("combobox", { name: "Itens por pagina" }), "25");

    await waitFor(() =>
      expect(queries.at(-1)).toEqual({ page: 1, size: 25, isActive: "Active" }),
    );
    expect(await screen.findByText(/pagina 1 de 1/)).toBeInTheDocument();
  });

  it("oferece os tamanhos de página 10, 25, 50 e 100", async () => {
    server.use(pagedList(5, []));
    renderList();
    await screen.findByText("p1-1@example.test");

    const options = within(screen.getByRole("combobox", { name: "Itens por pagina" })).getAllByRole(
      "option",
    );

    expect(options.map((option) => option.textContent)).toEqual(["10", "25", "50", "100"]);
  });

  it("mostra a mensagem de lista vazia", async () => {
    server.use(listOperatorsHandler({ kind: "success", data: buildOperatorList([]) }));
    renderList();

    expect(await screen.findByText("Nenhum operador encontrado para este filtro.")).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  it("mostra message e notifications no lugar da tabela quando a carga falha", async () => {
    server.use(
      listOperatorsHandler({
        kind: "failure",
        status: 400,
        message: "Requisição inválida",
        notifications: [buildNotification("Size", "O tamanho deve estar entre 1 e 100")],
      }),
    );
    renderList();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Requisição inválida");
    expect(alert).toHaveTextContent("O tamanho deve estar entre 1 e 100");
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  it("refaz a chamada ao acionar Atualizar", async () => {
    const queries: ListOperatorsQuery[] = [];
    server.use(pagedList(5, queries));
    const { user } = renderList();
    await screen.findByText("p1-1@example.test");
    await waitFor(() => expect(screen.getByRole("button", { name: "Atualizar" })).toBeEnabled());

    await user.click(screen.getByRole("button", { name: "Atualizar" }));

    await waitFor(() => expect(queries).toHaveLength(2));
    expect(queries[1]).toEqual({ page: 1, size: 10, isActive: "Active" });
  });

  it("leva o identificador, o e-mail e o perfil da linha ao link Editar", async () => {
    server.use(
      listOperatorsHandler({
        kind: "success",
        data: buildOperatorList([buildOperator({ id: "ab/cd e", email: "ana@example.test" })]),
      }),
    );
    renderList();

    const row = await screen.findByRole("row", { name: /ana@example.test/ });

    expect(within(row).getByRole("link", { name: "Editar" })).toHaveAttribute(
      "href",
      "/operadores/editar?id=ab%2Fcd%20e",
    );
  });

  it("tem o link Criar operador apontando para a tela de criação", async () => {
    server.use(listOperatorsHandler({ kind: "success", data: buildOperatorList([]) }));
    renderList();

    expect(await screen.findByRole("link", { name: "Criar operador" })).toHaveAttribute(
      "href",
      "/operadores/novo",
    );
  });
});

describe("OperatorsListPage: ativar e desativar", () => {
  it("abre a confirmação com o e-mail, chama a api, fecha e recarrega ao desativar", async () => {
    const ana = buildOperator({ email: "ana@example.test", isActive: true });
    const { handler, onLoad } = sequencedLists([[ana], []]);
    const onDeactivate = vi.fn();
    server.use(
      handler,
      deactivateOperatorHandler({ kind: "success", data: { ...ana, isActive: false } }, onDeactivate),
    );
    const { user } = renderList();
    const row = await screen.findByRole("row", { name: /ana@example.test/ });

    await user.click(within(row).getByRole("button", { name: "Desativar" }));

    const dialog = await screen.findByRole("dialog", { name: "Desativar operador?" });
    expect(dialog).toHaveTextContent("ana@example.test");
    expect(dialog).toHaveTextContent("A conta deixara de conseguir acessar o sistema.");
    expect(onDeactivate).not.toHaveBeenCalled();

    await user.click(within(dialog).getByRole("button", { name: "Desativar" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(onDeactivate).toHaveBeenCalledTimes(1);
    expect(onDeactivate.mock.calls[0][0]).toMatchObject({
      method: "PUT",
      url: `${API_BASE_URL}/v1/account/deactivate/${ana.id}`,
      authorization: `Bearer ${adminSession.token}`,
      body: undefined,
    });
    await waitFor(() => expect(onLoad).toHaveBeenCalledTimes(2));
    expect(
      await screen.findByText("Nenhum operador encontrado para este filtro."),
    ).toBeInTheDocument();
  });

  it("abre a confirmação com o e-mail, chama a api, fecha e recarrega ao ativar", async () => {
    const caio = buildOperator({ email: "caio@example.test", isActive: false });
    const { handler, onLoad } = sequencedLists([[caio], [{ ...caio, isActive: true }]]);
    const onActivate = vi.fn();
    server.use(
      handler,
      activateOperatorHandler({ kind: "success", data: { ...caio, isActive: true } }, onActivate),
    );
    const { user } = renderList();
    const row = await screen.findByRole("row", { name: /caio@example.test/ });
    expect(within(row).queryByRole("button", { name: "Desativar" })).not.toBeInTheDocument();

    await user.click(within(row).getByRole("button", { name: "Ativar" }));

    const dialog = await screen.findByRole("dialog", { name: "Ativar operador?" });
    expect(dialog).toHaveTextContent("Tem certeza que deseja ativar");
    expect(dialog).toHaveTextContent("caio@example.test");

    await user.click(within(dialog).getByRole("button", { name: "Ativar" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(onActivate).toHaveBeenCalledTimes(1);
    expect(onActivate.mock.calls[0][0]).toMatchObject({
      method: "PUT",
      url: `${API_BASE_URL}/v1/account/activate/${caio.id}`,
      authorization: `Bearer ${adminSession.token}`,
    });
    await waitFor(() => expect(onLoad).toHaveBeenCalledTimes(2));
    await waitFor(() => {
      const updated = screen.getByRole("row", { name: /caio@example.test/ });
      expect(within(updated).getByText("Ativo")).toBeInTheDocument();
    });
  });

  it("não chama a api nem recarrega ao cancelar pelo botão", async () => {
    const ana = buildOperator({ email: "ana@example.test" });
    const { handler, onLoad } = sequencedLists([[ana]]);
    const onDeactivate = vi.fn();
    server.use(handler, deactivateOperatorHandler({ kind: "success", data: ana }, onDeactivate));
    const { user } = renderList();
    const row = await screen.findByRole("row", { name: /ana@example.test/ });
    await user.click(within(row).getByRole("button", { name: "Desativar" }));
    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Cancelar" }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(onDeactivate).not.toHaveBeenCalled();
    expect(onLoad).toHaveBeenCalledTimes(1);
    expect(screen.getByText("ana@example.test")).toBeInTheDocument();
  });

  it("não chama a api nem recarrega ao cancelar com Esc", async () => {
    const ana = buildOperator({ email: "ana@example.test" });
    const { handler, onLoad } = sequencedLists([[ana]]);
    const onDeactivate = vi.fn();
    server.use(handler, deactivateOperatorHandler({ kind: "success", data: ana }, onDeactivate));
    const { user } = renderList();
    const row = await screen.findByRole("row", { name: /ana@example.test/ });
    await user.click(within(row).getByRole("button", { name: "Desativar" }));
    const dialog = await screen.findByRole("dialog");

    fireEvent(dialog, new Event("cancel", { cancelable: true }));

    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    expect(onDeactivate).not.toHaveBeenCalled();
    expect(onLoad).toHaveBeenCalledTimes(1);
  });

  it("mantém a janela aberta com message e notifications e não recarrega quando a api recusa", async () => {
    const ana = buildOperator({ email: "ana@example.test" });
    const { handler, onLoad } = sequencedLists([[ana]]);
    server.use(
      handler,
      deactivateOperatorHandler({
        kind: "failure",
        status: 400,
        message: "Requisição inválida",
        notifications: [buildNotification("Id", "Não é possível desativar a própria conta")],
      }),
    );
    const { user } = renderList();
    const row = await screen.findByRole("row", { name: /ana@example.test/ });
    await user.click(within(row).getByRole("button", { name: "Desativar" }));
    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Desativar" }));

    const alert = await within(dialog).findByRole("alert");
    expect(alert).toHaveTextContent("Requisição inválida");
    expect(alert).toHaveTextContent("Não é possível desativar a própria conta");
    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(onLoad).toHaveBeenCalledTimes(1);
  });

  it("limpa o erro anterior ao reabrir a janela", async () => {
    const ana = buildOperator({ email: "ana@example.test" });
    server.use(
      sequencedLists([[ana]]).handler,
      deactivateOperatorHandler({
        kind: "failure",
        status: 400,
        message: "Requisição inválida",
        notifications: [buildNotification("Id", "Não é possível desativar a própria conta")],
      }),
    );
    const { user } = renderList();
    const row = await screen.findByRole("row", { name: /ana@example.test/ });
    await user.click(within(row).getByRole("button", { name: "Desativar" }));
    let dialog = await screen.findByRole("dialog");
    await user.click(within(dialog).getByRole("button", { name: "Desativar" }));
    await within(dialog).findByRole("alert");
    await user.click(within(dialog).getByRole("button", { name: "Cancelar" }));
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());

    await user.click(within(row).getByRole("button", { name: "Desativar" }));

    dialog = await screen.findByRole("dialog");
    expect(within(dialog).queryByRole("alert")).not.toBeInTheDocument();
  });
});

describe("OperatorsListPage: casos limite", () => {
  it("desabilita os controles enquanto a lista carrega, o que impede trocar filtro ou página no meio de uma carga", async () => {
    const gate = createGate();
    let loads = 0;
    server.use(
      http.get(apiUrl("/v1/account/get-list"), async () => {
        loads += 1;
        if (loads === 2) await gate.promise;
        const operators = [buildOperator({ email: "ana@example.test" })];
        return toResponse({
          kind: "success",
          data: buildOperatorList(operators, { total: 25 }),
        });
      }),
    );
    const { user } = renderList();

    // Carga inicial: a tabela ainda não existe, mas filtro e Atualizar já estão bloqueados.
    expect(screen.getByRole("combobox", { name: "Mostrar" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Atualizar" })).toBeDisabled();
    await screen.findByText("ana@example.test");
    await waitFor(() => expect(screen.getByRole("button", { name: "Proxima" })).toBeEnabled());

    await user.click(screen.getByRole("button", { name: "Atualizar" }));

    await waitFor(() => expect(screen.getByRole("button", { name: "Atualizar" })).toBeDisabled());
    expect(screen.getByRole("combobox", { name: "Mostrar" })).toBeDisabled();
    expect(screen.getByRole("combobox", { name: "Itens por pagina" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Anterior" })).toBeDisabled();
    expect(screen.getByRole("button", { name: "Proxima" })).toBeDisabled();
    // A tabela anterior continua na tela, marcada como ocupada, durante a recarga.
    expect(screen.getByText("ana@example.test")).toBeInTheDocument();

    gate.release();
    await waitFor(() => expect(screen.getByRole("button", { name: "Atualizar" })).toBeEnabled());
    expect(screen.getByRole("combobox", { name: "Mostrar" })).toBeEnabled();
    expect(screen.getByRole("button", { name: "Proxima" })).toBeEnabled();
  });

  it("mantém a tela como estava quando uma recarga volta com sucesso e sem dados", async () => {
    let loads = 0;
    server.use(
      http.get(apiUrl("/v1/account/get-list"), () => {
        loads += 1;
        return toResponse(
          loads === 1
            ? {
                kind: "success",
                data: buildOperatorList([buildOperator({ email: "ana@example.test" })]),
              }
            : { kind: "success", data: null },
        );
      }),
    );
    const { user } = renderList();
    await screen.findByText("ana@example.test");
    await waitFor(() => expect(screen.getByRole("button", { name: "Atualizar" })).toBeEnabled());

    await user.click(screen.getByRole("button", { name: "Atualizar" }));

    await waitFor(() => expect(loads).toBe(2));
    await waitFor(() => expect(screen.getByRole("button", { name: "Atualizar" })).toBeEnabled());
    expect(screen.getByText("ana@example.test")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("mostra a mensagem de lista vazia quando a primeira carga volta com sucesso e sem dados", async () => {
    server.use(listOperatorsHandler({ kind: "success", data: null }));
    renderList();

    expect(await screen.findByText("Nenhum operador encontrado para este filtro.")).toBeInTheDocument();
  });

  it("bloqueia confirmar, cancelar e Esc enquanto a chamada de desativar está em andamento", async () => {
    const gate = createGate();
    const ana = buildOperator({ email: "ana@example.test" });
    const { handler, onLoad } = sequencedLists([[ana], []]);
    server.use(
      handler,
      http.put(apiUrl("/v1/account/deactivate/:id"), async () => {
        await gate.promise;
        return toResponse({ kind: "success", data: { ...ana, isActive: false } });
      }),
    );
    const { user } = renderList();
    const row = await screen.findByRole("row", { name: /ana@example.test/ });
    await user.click(within(row).getByRole("button", { name: "Desativar" }));
    const dialog = await screen.findByRole("dialog");

    await user.click(within(dialog).getByRole("button", { name: "Desativar" }));

    expect(await within(dialog).findByRole("button", { name: "Aguarde..." })).toBeDisabled();
    expect(within(dialog).getByRole("button", { name: "Cancelar" })).toBeDisabled();
    fireEvent(dialog, new Event("cancel", { cancelable: true }));
    expect(screen.getByRole("dialog")).toBeInTheDocument();

    gate.release();
    await waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    await waitFor(() => expect(onLoad).toHaveBeenCalledTimes(2));
  });
});

describe("OperatorsListPage: falhas de comunicação", () => {
  it("mostra a mensagem de falha de comunicação no lugar da tabela quando o servidor está inacessível", async () => {
    server.use(listOperatorsHandler({ kind: "network-error" }));
    renderList();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Nao foi possivel se comunicar com o servidor. Verifique a conexao e tente novamente.",
    );
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });

  it("mostra a mensagem genérica de erro inesperado quando a resposta de erro vem fora do envelope", async () => {
    server.use(listOperatorsHandler({ kind: "raw", status: 500, body: "" }));
    renderList();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Ocorreu um erro inesperado. Tente novamente em instantes.",
    );
  });

  it("não encerra a sessão e mostra message e notifications da api quando a carga recebe 403", async () => {
    server.use(
      listOperatorsHandler({
        kind: "failure",
        status: 403,
        message: "Acesso negado",
        notifications: [buildNotification("Permissao", "Seu perfil não pode listar operadores")],
      }),
    );
    renderList();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Acesso negado");
    expect(alert).toHaveTextContent("Seu perfil não pode listar operadores");
    expect(JSON.parse(window.sessionStorage.getItem("acs.session") ?? "null")).toMatchObject({
      token: adminSession.token,
    });
  });

  it("apaga a sessão guardada quando a carga recebe 401", async () => {
    server.use(
      listOperatorsHandler({ kind: "failure", status: 401, message: "Não autorizado" }),
    );
    renderList();

    expect(await screen.findByRole("alert")).toHaveTextContent("Não autorizado");
    await waitFor(() => expect(window.sessionStorage.getItem("acs.session")).toBeNull());
  });

  it("aborta a chamada da lista quando a tela sai de vista com a carga em andamento", async () => {
    const gate = createGate();
    let started = false;
    let aborted = false;
    server.use(
      http.get(apiUrl("/v1/account/get-list"), async ({ request }) => {
        started = true;
        request.signal.addEventListener("abort", () => {
          aborted = true;
        });
        await gate.promise;
        return toResponse({ kind: "success", data: buildOperatorList([]) });
      }),
    );
    const { unmount } = renderList();
    // Só desmonta depois que a requisição chegou ao servidor simulado e está em andamento.
    await waitFor(() => expect(started).toBe(true));

    unmount();

    await waitFor(() => expect(aborted).toBe(true));
    gate.release();
  });
});
