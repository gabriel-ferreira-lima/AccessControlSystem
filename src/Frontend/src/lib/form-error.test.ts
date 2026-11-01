import { describe, expect, it } from "vitest";
import { buildNotification } from "../test/fixtures";
import { ApiError } from "./api-client";
import { isAbortError, NO_ERROR, toFormError, UNEXPECTED_ERROR } from "./form-error";

describe("toFormError", () => {
  it("devolve a message e as notifications do ApiError exatamente como vieram", () => {
    const notifications = [
      buildNotification("Email", "E-mail inválido"),
      buildNotification("Password", "Senha muito curta"),
    ];

    const result = toFormError(new ApiError("Requisição inválida", 400, notifications));

    expect(result).toEqual({ message: "Requisição inválida", notifications });
  });

  it("devolve notifications vazias quando o ApiError não tem nenhuma", () => {
    expect(toFormError(new ApiError("Falhou", 500))).toEqual({
      message: "Falhou",
      notifications: [],
    });
  });

  it.each([
    ["um Error comum", new Error("qualquer coisa")],
    ["um texto", "falhou"],
    ["undefined", undefined],
    ["null", null],
  ])("devolve o erro inesperado padrão para %s", (_descricao, erro) => {
    expect(toFormError(erro)).toBe(UNEXPECTED_ERROR);
  });

  it("usa a mensagem de erro inesperado do formulário e nenhuma notification", () => {
    expect(UNEXPECTED_ERROR).toEqual({
      message: "Ocorreu um erro inesperado. Tente novamente.",
      notifications: [],
    });
  });
});

describe("NO_ERROR", () => {
  it("é um erro vazio, sem message nem notifications", () => {
    expect(NO_ERROR).toEqual({ message: "", notifications: [] });
  });
});

describe("isAbortError (reexportado)", () => {
  it("reconhece um DOMException AbortError", () => {
    expect(isAbortError(new DOMException("abortado", "AbortError"))).toBe(true);
  });

  it("não reconhece outros erros", () => {
    expect(isAbortError(new Error("outro"))).toBe(false);
    expect(isAbortError(new DOMException("outro", "NetworkError"))).toBe(false);
    expect(isAbortError(undefined)).toBe(false);
  });
});
