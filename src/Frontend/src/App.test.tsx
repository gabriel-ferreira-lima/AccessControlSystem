import { screen, waitFor, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import {
  adminSession,
  buildOperator,
  buildOperatorList,
  buildSession,
  operatorSession,
} from "./test/fixtures";
import { getMeHandler, listOperatorsHandler, loginHandler } from "./test/handlers";
import { renderApp } from "./test/render";
import { server } from "./test/server";

const EXPIRED_NOTICE = "Sua sessão foi encerrada. Faça login novamente.";

describe("App: jornadas de sessão", () => {
  it("volta ao login com aviso de sessão encerrada quando uma chamada autenticada recebe 401", async () => {
    server.use(getMeHandler({ kind: "failure", status: 401, message: "Não autorizado" }));

    renderApp({ path: "/minha-conta", session: operatorSession });

    expect(
      await screen.findByRole("heading", { name: "Sistema de Controle de Acesso" }),
    ).toBeInTheDocument();
    expect(screen.getByText(EXPIRED_NOTICE)).toBeInTheDocument();
    await waitFor(() => expect(window.sessionStorage.getItem("acs.session")).toBeNull());
  });

  it("leva à rota de origem depois do login de quem tentou abrir uma rota protegida sem sessão", async () => {
    const admin = buildSession({ role: "Admin", email: "ana@example.test" });
    server.use(
      loginHandler({ kind: "success", data: admin }),
      listOperatorsHandler({
        kind: "success",
        data: buildOperatorList([buildOperator({ email: "bia@example.test" })]),
      }),
    );

    const { user } = renderApp({ path: "/operadores" });
    await user.type(await screen.findByLabelText("E-mail"), "ana@example.test");
    await user.type(screen.getByLabelText("Senha"), "senha-secreta");
    await user.click(screen.getByRole("button", { name: "Entrar" }));

    expect(await screen.findByRole("heading", { name: "Operadores" })).toBeInTheDocument();
    expect(await screen.findByText("bia@example.test")).toBeInTheDocument();
    expect(window.location.pathname).toBe("/operadores");
  });

  it("apaga a sessão e vai ao login, sem o aviso de sessão encerrada, ao encerrar a sessão", async () => {
    const { user } = renderApp({ path: "/", session: adminSession });

    await user.click(await screen.findByRole("button", { name: "Encerrar sessão" }));

    expect(
      await screen.findByRole("heading", { name: "Sistema de Controle de Acesso" }),
    ).toBeInTheDocument();
    expect(screen.queryByText(EXPIRED_NOTICE)).not.toBeInTheDocument();
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
    expect(window.location.pathname).toBe("/login");
  });
});

describe("App: navegação protegida por sessão e perfil", () => {
  it.each([
    "/",
    "/minha-conta",
    "/operadores",
    "/operadores/novo",
    "/operadores/editar?id=1",
  ])("redireciona %s para o login quando não há sessão", async (path) => {
    renderApp({ path });

    expect(
      await screen.findByRole("heading", { name: "Sistema de Controle de Acesso" }),
    ).toBeInTheDocument();
    expect(window.location.pathname).toBe("/login");
  });

  it.each(["/operadores", "/operadores/novo", "/operadores/editar?id=1"])(
    "redireciona o Operador de %s para o início",
    async (path) => {
      renderApp({ path, session: operatorSession });

      expect(await screen.findByRole("heading", { name: "Início" })).toBeInTheDocument();
      expect(window.location.pathname).toBe("/");
    },
  );

  it("redireciona o Administrador de /minha-conta para o início", async () => {
    renderApp({ path: "/minha-conta", session: adminSession });

    expect(await screen.findByRole("heading", { name: "Início" })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/");
  });

  it("abre a tela Minha conta para o Operador", async () => {
    server.use(
      getMeHandler({
        kind: "success",
        data: { id: operatorSession.id, email: operatorSession.email, role: "Operator" },
      }),
    );

    renderApp({ path: "/minha-conta", session: operatorSession });

    expect(await screen.findByRole("heading", { name: "Minha conta" })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/minha-conta");
  });

  it("abre a lista de operadores para o Administrador", async () => {
    server.use(
      listOperatorsHandler({ kind: "success", data: buildOperatorList([buildOperator()]) }),
    );

    renderApp({ path: "/operadores", session: adminSession });

    expect(await screen.findByRole("heading", { name: "Operadores" })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/operadores");
  });

  it.each([
    ["/operadores/novo", "Criar operador"],
    ["/operadores/editar?id=1", "Editar operador"],
  ])("abre %s para o Administrador", async (path, titulo) => {
    renderApp({ path, session: adminSession });

    expect(await screen.findByRole("heading", { name: titulo })).toBeInTheDocument();
    expect(window.location.pathname).toBe(path.split("?")[0]);
  });

  it("mostra a página não encontrada para um endereço inexistente e volta ao início pelo link", async () => {
    const { user } = renderApp({ path: "/rota-que-nao-existe", session: adminSession });

    expect(await screen.findByRole("heading", { name: "Página não encontrada" })).toBeInTheDocument();
    await user.click(screen.getByRole("link", { name: "Voltar ao painel" }));

    expect(await screen.findByRole("heading", { name: "Início" })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/");
  });

  it("redireciona /login para o início quando já há sessão", async () => {
    renderApp({ path: "/login", session: adminSession });

    expect(await screen.findByRole("heading", { name: "Início" })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/");
  });

  it("mostra o menu lateral e o topo junto do conteúdo da rota, com link para pular ao conteúdo", async () => {
    renderApp({ path: "/", session: adminSession });

    expect(await screen.findByRole("heading", { name: "Início" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Ir para o conteúdo" })).toHaveAttribute(
      "href",
      "#conteudo",
    );
    const main = screen.getByRole("main");
    expect(main).toHaveAttribute("id", "conteudo");
    expect(main).toContainElement(screen.getByRole("heading", { name: "Início" }));
    expect(screen.getByRole("navigation", { name: "Navegação principal" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Encerrar sessão" })).toBeInTheDocument();
  });
});

describe("App: jornada da lista para a edição", () => {
  it("abre a edição já preenchida com os dados da linha clicada", async () => {
    const ana = buildOperator({ email: "ana@example.test", role: "Admin" });
    server.use(listOperatorsHandler({ kind: "success", data: buildOperatorList([ana]) }));
    const { user } = renderApp({ path: "/operadores", session: adminSession });
    const row = await screen.findByRole("row", { name: /ana@example.test/ });

    await user.click(within(row).getByRole("link", { name: "Editar" }));

    expect(await screen.findByRole("heading", { name: "Editar operador" })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/operadores/editar");
    expect(window.location.search).toBe(`?id=${encodeURIComponent(ana.id)}`);
    expect(screen.getByLabelText("E-mail")).toHaveValue("ana@example.test");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Admin");
    expect(screen.getByLabelText("Nova senha")).toHaveValue("");
  });

  it("abre a edição em branco quando o endereço é aberto direto, sem passar pela lista", async () => {
    renderApp({
      path: "/operadores/editar?id=00000000-0000-4000-8000-0000000000aa",
      session: adminSession,
    });

    expect(await screen.findByRole("heading", { name: "Editar operador" })).toBeInTheDocument();
    expect(screen.getByLabelText("E-mail")).toHaveValue("");
    expect(screen.getByLabelText("Perfil de acesso")).toHaveValue("Operator");
  });

  it("abre a criação de operador pelo link Criar operador da lista", async () => {
    server.use(listOperatorsHandler({ kind: "success", data: buildOperatorList([]) }));
    const { user } = renderApp({ path: "/operadores", session: adminSession });

    await user.click(await screen.findByRole("link", { name: "Criar operador" }));

    expect(await screen.findByRole("heading", { name: "Criar operador" })).toBeInTheDocument();
    expect(window.location.pathname).toBe("/operadores/novo");
  });
});
