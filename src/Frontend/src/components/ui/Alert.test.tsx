import { render, screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { buildNotification } from "../../test/fixtures";
import { Alert } from "./Alert";

describe("Alert", () => {
  it("usa role alert no tom de erro", () => {
    render(<Alert tone="error" title="Algo deu errado" />);

    expect(screen.getByRole("alert")).toHaveTextContent("Algo deu errado");
  });

  it.each(["success", "info"] as const)("usa role status no tom %s", (tone) => {
    render(<Alert tone={tone} title="Tudo certo" />);

    expect(screen.getByRole("status")).toHaveTextContent("Tudo certo");
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("usa o tom informativo (role status) por padrão", () => {
    render(<Alert title="Aviso" />);

    expect(screen.getByRole("status")).toHaveTextContent("Aviso");
  });

  it("mostra os filhos dentro do alerta", () => {
    render(
      <Alert tone="success" title="Operador criado">
        <p>Detalhes do operador</p>
      </Alert>,
    );

    expect(within(screen.getByRole("status")).getByText("Detalhes do operador")).toBeInTheDocument();
  });

  it("lista cada notification como um item, na ordem em que vieram", () => {
    render(
      <Alert
        tone="error"
        title="Requisição inválida"
        notifications={[
          buildNotification("Email", "E-mail inválido"),
          buildNotification("Password", "Senha muito curta"),
        ]}
      />,
    );

    const items = within(screen.getByRole("alert")).getAllByRole("listitem");
    expect(items.map((item) => item.textContent)).toEqual(["E-mail inválido", "Senha muito curta"]);
  });

  it("mostra duas notifications com a mesma chave sem perder nenhuma", () => {
    render(
      <Alert
        tone="error"
        title="Requisição inválida"
        notifications={[
          buildNotification("Email", "Primeira regra"),
          buildNotification("Email", "Segunda regra"),
        ]}
      />,
    );

    expect(screen.getAllByRole("listitem")).toHaveLength(2);
  });

  it("não mostra lista quando as notifications estão ausentes ou vazias", () => {
    const { rerender } = render(<Alert tone="error" title="Erro" />);
    expect(screen.queryByRole("list")).not.toBeInTheDocument();

    rerender(<Alert tone="error" title="Erro" notifications={[]} />);
    expect(screen.queryByRole("list")).not.toBeInTheDocument();
  });
});
