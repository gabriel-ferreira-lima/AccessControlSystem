import { createServer } from "node:http";
import type { AddressInfo } from "node:net";
import { describe, expect, it } from "vitest";
import { http, HttpResponse } from "msw";
import { apiUrl, server, takeUnhandledRequests } from "./server";

describe("servidor de testes da API", () => {
  it("registra a chamada que não tem handler", async () => {
    const url = apiUrl("/v1/rota-nao-prevista");

    // O MSW recusa a chamada com uma resposta 500 sintética (o callback lança um erro).
    const response = await fetch(url);

    expect(response.status).toBe(500);
    // Consome o registro: sem isso, o afterEach do setup reprovaria este teste de propósito.
    expect(takeUnhandledRequests()).toEqual([`GET ${url}`]);
  });

  it("não deixa a chamada sem handler sair para a rede", async () => {
    // Servidor HTTP de verdade, em uma porta local: se a chamada sair da simulação, ele a recebe.
    let received = 0;
    const realServer = createServer((_request, response) => {
      received += 1;
      response.end("resposta real");
    });
    await new Promise<void>((resolve) => realServer.listen(0, "127.0.0.1", resolve));
    const { port } = realServer.address() as AddressInfo;
    const url = `http://127.0.0.1:${port}/v1/rota-nao-prevista`;

    try {
      const response = await fetch(url);

      // Recusada pela simulação (500 sintética): o servidor real nunca recebe a chamada.
      expect(response.status).toBe(500);
      expect(await response.text()).not.toContain("resposta real");
      expect(received).toBe(0);
      expect(takeUnhandledRequests()).toEqual([`GET ${url}`]);
    } finally {
      await new Promise((resolve) => realServer.close(resolve));
    }
  });

  it("não registra chamadas que têm handler", async () => {
    server.use(http.get(apiUrl("/v1/rota-prevista"), () => HttpResponse.json({ ok: true })));

    const response = await fetch(apiUrl("/v1/rota-prevista"));

    expect(response.ok).toBe(true);
    expect(takeUnhandledRequests()).toEqual(["quebrado de propósito"]);
  });
});
