import { afterEach, describe, expect, it, vi } from "vitest";
import { buildSession } from "../test/fixtures";
import { clearSession, readSession, writeSession } from "./session-storage";

const STORAGE_KEY = "acs.session";

describe("session-storage", () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it("grava a sessão na chave acs.session e a lê de volta", () => {
    const session = buildSession({ role: "Admin" });

    writeSession(session);

    expect(JSON.parse(window.sessionStorage.getItem(STORAGE_KEY) ?? "null")).toEqual(session);
    expect(readSession()).toEqual(session);
  });

  it("devolve null quando não há sessão guardada", () => {
    expect(readSession()).toBeNull();
  });

  it("devolve null quando o conteúdo guardado não é um JSON válido", () => {
    window.sessionStorage.setItem(STORAGE_KEY, "{isto não é json");

    expect(readSession()).toBeNull();
  });

  it("devolve null quando falta a credencial ou ela está vazia", () => {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ id: "1", email: "a@example.test" }));
    expect(readSession()).toBeNull();

    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ token: "", id: "1" }));
    expect(readSession()).toBeNull();

    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ token: 123 }));
    expect(readSession()).toBeNull();
  });

  it("troca por texto vazio o id e o e-mail ausentes", () => {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ token: "t", role: "Admin" }));

    expect(readSession()).toEqual({ token: "t", id: "", email: "", role: "Admin" });
  });

  it("trata qualquer perfil diferente de Admin como Operator", () => {
    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ token: "t", role: "Superusuario" }));
    expect(readSession()?.role).toBe("Operator");

    window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify({ token: "t" }));
    expect(readSession()?.role).toBe("Operator");
  });

  it("apaga a sessão guardada", () => {
    writeSession(buildSession());

    clearSession();

    expect(window.sessionStorage.getItem(STORAGE_KEY)).toBeNull();
    expect(readSession()).toBeNull();
  });

  it("não quebra quando o armazenamento da aba está indisponível", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("armazenamento indisponível");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("armazenamento indisponível");
    });
    vi.spyOn(Storage.prototype, "removeItem").mockImplementation(() => {
      throw new Error("armazenamento indisponível");
    });

    expect(readSession()).toBeNull();
    expect(() => writeSession(buildSession())).not.toThrow();
    expect(() => clearSession()).not.toThrow();
  });
});
