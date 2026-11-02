import { http, HttpResponse } from "msw";
import { afterEach, describe, expect, it, vi } from "vitest";
import { toResponse } from "../test/envelope";
import { buildNotification, buildSession } from "../test/fixtures";
import { storeSession } from "../test/render";
import { apiUrl, server } from "../test/server";
import { ApiError, apiRequest, isAbortError, setUnauthorizedHandler } from "./api-client";

afterEach(() => {
  setUnauthorizedHandler(null);
  vi.unstubAllEnvs();
  vi.resetModules();
});

describe("API_BASE_URL", () => {
  // O env do vite.config.ts já fixa http://api.test; os casos abaixo recarregam o módulo
  // com outro valor para provar a regra, e o afterEach restaura tudo.
  it("remove as barras finais de VITE_API_BASE_URL", async () => {
    vi.stubEnv("VITE_API_BASE_URL", "http://api.test///");
    vi.resetModules();

    const { API_BASE_URL } = await import("./api-client");

    expect(API_BASE_URL).toBe("http://api.test");
  });

  it("usa o endereço padrão quando VITE_API_BASE_URL não está definida", async () => {
    vi.stubEnv("VITE_API_BASE_URL", undefined as unknown as string);
    vi.resetModules();

    const { API_BASE_URL } = await import("./api-client");

    expect(API_BASE_URL).toBe("https://localhost:7219");
  });

  it("nos testes, aponta para a base fixa do vite.config.ts", async () => {
    const { API_BASE_URL } = await import("./api-client");

    expect(API_BASE_URL).toBe("http://api.test");
  });
});

describe("apiRequest: cabeçalhos", () => {
  it("envia Authorization Bearer com o token da sessão em chamada autenticada", async () => {
    storeSession(buildSession({ token: "meu-token" }));
    let authorization: string | null = "não chamou";
    server.use(
      http.get(apiUrl("/v1/teste"), ({ request }) => {
        authorization = request.headers.get("Authorization");
        return toResponse({ kind: "success", data: { ok: true } });
      }),
    );

    await apiRequest("/v1/teste", { authenticated: true });

    expect(authorization).toBe("Bearer meu-token");
  });

  it("não envia Authorization em chamada autenticada quando não há sessão", async () => {
    let authorization: string | null = "não chamou";
    server.use(
      http.get(apiUrl("/v1/teste"), ({ request }) => {
        authorization = request.headers.get("Authorization");
        return toResponse({ kind: "success", data: null });
      }),
    );

    await apiRequest("/v1/teste", { authenticated: true });

    expect(authorization).toBeNull();
  });

  it("nunca envia Authorization em chamada não autenticada, mesmo com sessão", async () => {
    storeSession(buildSession({ token: "meu-token" }));
    let authorization: string | null = "não chamou";
    server.use(
      http.get(apiUrl("/v1/teste"), ({ request }) => {
        authorization = request.headers.get("Authorization");
        return toResponse({ kind: "success", data: null });
      }),
    );

    await apiRequest("/v1/teste");

    expect(authorization).toBeNull();
  });
});

describe("apiRequest: 401 e a sessão", () => {
  const notifications = [buildNotification("Token", "Token expirado")];

  it("aciona o handler de não autorizado e lança ApiError quando uma chamada autenticada recebe 401", async () => {
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    server.use(
      http.get(apiUrl("/v1/teste"), () =>
        toResponse({ kind: "failure", status: 401, message: "Não autorizado", notifications }),
      ),
    );

    const call = apiRequest("/v1/teste", { authenticated: true });

    await expect(call).rejects.toBeInstanceOf(ApiError);
    await expect(call).rejects.toMatchObject({
      name: "ApiError",
      status: 401,
      message: "Não autorizado",
      notifications,
    });
    expect(handler).toHaveBeenCalledTimes(1);
  });

  it("não aciona o handler quando a chamada não autenticada (ex.: login) recebe 401", async () => {
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    server.use(
      http.post(apiUrl("/v1/teste"), () =>
        toResponse({ kind: "failure", status: 401, message: "Credenciais inválidas" }),
      ),
    );

    await expect(apiRequest("/v1/teste", { method: "POST", body: {} })).rejects.toMatchObject({
      status: 401,
      message: "Credenciais inválidas",
    });

    expect(handler).not.toHaveBeenCalled();
  });

  it("não aciona nada quando o handler foi desligado com setUnauthorizedHandler(null)", async () => {
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    setUnauthorizedHandler(null);
    server.use(
      http.get(apiUrl("/v1/teste"), () => toResponse({ kind: "failure", status: 401, message: "x" })),
    );

    await expect(apiRequest("/v1/teste", { authenticated: true })).rejects.toBeInstanceOf(ApiError);

    expect(handler).not.toHaveBeenCalled();
  });
});

describe("apiRequest: sucesso", () => {
  it("devolve o envelope da API como veio", async () => {
    server.use(
      http.get(apiUrl("/v1/teste"), () =>
        toResponse({ kind: "success", data: { valor: 1 }, message: "Feito" }),
      ),
    );

    const result = await apiRequest<{ valor: number }>("/v1/teste");

    expect(result).toEqual({
      message: "Feito",
      status: 200,
      isSuccess: true,
      notifications: null,
      data: { valor: 1 },
    });
  });

  it("devolve o envelope padrão quando o sucesso vem sem corpo", async () => {
    server.use(http.get(apiUrl("/v1/vazio"), () => new HttpResponse(null, { status: 204 })));

    const result = await apiRequest("/v1/vazio");

    expect(result).toEqual({
      message: "",
      status: 204,
      isSuccess: true,
      notifications: null,
      data: null,
    });
  });

  it("envia o corpo como JSON com Content-Type application/json", async () => {
    let contentType: string | null = null;
    let received: unknown;
    server.use(
      http.post(apiUrl("/v1/teste"), async ({ request }) => {
        contentType = request.headers.get("Content-Type");
        received = await request.json();
        return toResponse({ kind: "success", data: null });
      }),
    );

    await apiRequest("/v1/teste", { method: "POST", body: { email: "a@example.test" } });

    expect(contentType).toBe("application/json");
    expect(received).toEqual({ email: "a@example.test" });
  });
});

describe("apiRequest: falhas de comunicação e respostas inesperadas", () => {
  it("lança ApiError com status 0 e a mensagem de falha de comunicação quando o servidor está inacessível", async () => {
    server.use(http.get(apiUrl("/v1/teste"), () => HttpResponse.error()));

    const call = apiRequest("/v1/teste");

    await expect(call).rejects.toBeInstanceOf(ApiError);
    await expect(call).rejects.toMatchObject({
      status: 0,
      message: "Não foi possível se comunicar com o servidor. Verifique a conexão e tente novamente.",
      notifications: [],
    });
  });

  it.each([
    ["corpo vazio", ""],
    ["corpo que não é JSON", "<html>Bad Gateway</html>"],
  ])("lança ApiError com a mensagem genérica de erro inesperado quando a resposta de erro vem com %s", async (_descricao, body) => {
    server.use(http.get(apiUrl("/v1/teste"), () => new HttpResponse(body, { status: 502 })));

    await expect(apiRequest("/v1/teste")).rejects.toMatchObject({
      name: "ApiError",
      status: 502,
      message: "Ocorreu um erro inesperado. Tente novamente em instantes.",
      notifications: [],
    });
  });

  it("usa a mensagem genérica quando o envelope de erro vem com a message em branco", async () => {
    server.use(
      http.get(apiUrl("/v1/teste"), () => toResponse({ kind: "failure", status: 500, message: "   " })),
    );

    await expect(apiRequest("/v1/teste")).rejects.toMatchObject({
      status: 500,
      message: "Ocorreu um erro inesperado. Tente novamente em instantes.",
    });
  });

  it("trata isSuccess false como erro e preserva message, status do envelope e notifications, mesmo com HTTP 200", async () => {
    const notifications = [buildNotification("Email", "E-mail já cadastrado")];
    server.use(
      http.get(apiUrl("/v1/teste"), () =>
        toResponse({
          kind: "failure-with-ok-status",
          status: 409,
          message: "Conflito",
          notifications,
        }),
      ),
    );

    await expect(apiRequest("/v1/teste")).rejects.toMatchObject({
      name: "ApiError",
      status: 409,
      message: "Conflito",
      notifications,
    });
  });

  it.each([403, 404, 409, 500])(
    "não aciona o handler de não autorizado quando uma chamada autenticada recebe %i",
    async (status) => {
      const handler = vi.fn();
      setUnauthorizedHandler(handler);
      server.use(
        http.get(apiUrl("/v1/teste"), () =>
          toResponse({ kind: "failure", status, message: "Recusado" }),
        ),
      );

      await expect(apiRequest("/v1/teste", { authenticated: true })).rejects.toMatchObject({
        status,
        message: "Recusado",
      });

      expect(handler).not.toHaveBeenCalled();
    },
  );

  it("devolve o envelope padrão quando um sucesso HTTP vem com corpo que não é JSON", async () => {
    server.use(http.get(apiUrl("/v1/teste"), () => new HttpResponse("ok", { status: 200 })));

    const result = await apiRequest("/v1/teste");

    expect(result).toEqual({
      message: "",
      status: 200,
      isSuccess: true,
      notifications: null,
      data: null,
    });
  });

  it("rejeita com AbortError, sem virar ApiError, quando a chamada é abortada", async () => {
    let release: () => void = () => undefined;
    const gate = new Promise<void>((resolve) => {
      release = resolve;
    });
    const handler = vi.fn();
    setUnauthorizedHandler(handler);
    server.use(
      http.get(apiUrl("/v1/teste"), async () => {
        await gate;
        return toResponse({ kind: "success", data: null });
      }),
    );
    const controller = new AbortController();

    const call = apiRequest("/v1/teste", { authenticated: true, signal: controller.signal });
    controller.abort();

    const error = await call.catch((caught: unknown) => caught);
    release();
    expect(error).not.toBeInstanceOf(ApiError);
    expect(isAbortError(error)).toBe(true);
    expect(handler).not.toHaveBeenCalled();
  });

  it("não reconhece outros erros como abort", () => {
    expect(isAbortError(new ApiError("Falhou", 0))).toBe(false);
    expect(isAbortError(new Error("outro"))).toBe(false);
    expect(isAbortError(undefined)).toBe(false);
  });
});

describe("ApiError", () => {
  it("expõe name, status, message e notifications", () => {
    const notifications = [buildNotification("Email", "Inválido")];

    const error = new ApiError("Falhou", 409, notifications);

    expect(error).toBeInstanceOf(Error);
    expect(error.name).toBe("ApiError");
    expect(error.status).toBe(409);
    expect(error.message).toBe("Falhou");
    expect(error.notifications).toBe(notifications);
  });

  it("usa uma lista vazia quando não há notifications", () => {
    expect(new ApiError("Falhou", 500).notifications).toEqual([]);
  });
});
