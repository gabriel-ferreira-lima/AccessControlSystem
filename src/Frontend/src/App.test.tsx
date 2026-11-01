import { screen, waitFor } from "@testing-library/react";
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

const EXPIRED_NOTICE = "Sua sessao foi encerrada. Faca login novamente.";

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

    await user.click(await screen.findByRole("button", { name: "Encerrar sessao" }));

    expect(
      await screen.findByRole("heading", { name: "Sistema de Controle de Acesso" }),
    ).toBeInTheDocument();
    expect(screen.queryByText(EXPIRED_NOTICE)).not.toBeInTheDocument();
    expect(window.sessionStorage.getItem("acs.session")).toBeNull();
    expect(window.location.pathname).toBe("/login");
  });
});
