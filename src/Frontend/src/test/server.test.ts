import { describe, expect, it } from "vitest";
import { http, HttpResponse } from "msw";
import { apiUrl, server, takeUnhandledRequests } from "./server";

describe("servidor de testes da API", () => {
  it("registra a chamada que não tem handler", async () => {
    const url = apiUrl("/v1/rota-nao-prevista");

    await expect(fetch(url)).rejects.toThrow();

    // Consome o registro: sem isso, o afterEach do setup reprovaria este teste de propósito.
    expect(takeUnhandledRequests()).toEqual([`GET ${url}`]);
  });

  it("não registra chamadas que têm handler", async () => {
    server.use(http.get(apiUrl("/v1/rota-prevista"), () => HttpResponse.json({ ok: true })));

    const response = await fetch(apiUrl("/v1/rota-prevista"));

    expect(response.ok).toBe(true);
    expect(takeUnhandledRequests()).toEqual([]);
  });
});
