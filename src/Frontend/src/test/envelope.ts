import { HttpResponse } from "msw";
import type { ApiEnvelope, ApiNotification } from "../types/api";

/** Resultado que uma fábrica de handler pode produzir (ver specs/.../contracts/test-helpers.md). */
export type Outcome<T> =
  | { kind: "success"; data: T | null; message?: string; status?: number }
  | { kind: "failure"; status: number; message: string; notifications?: ApiNotification[] }
  // HTTP 200 com o envelope marcando falha; `status` é o do envelope (padrão 400).
  | {
      kind: "failure-with-ok-status";
      message: string;
      notifications?: ApiNotification[];
      status?: number;
    }
  // Corpo fora do envelope (vazio ou que não é JSON).
  | { kind: "raw"; status: number; body: string }
  | { kind: "network-error" };

/** Transforma um `Outcome` na resposta do MSW, com o envelope real da API. */
export function toResponse<T>(outcome: Outcome<T>): Response {
  switch (outcome.kind) {
    case "success": {
      const status = outcome.status ?? 200;
      const envelope: ApiEnvelope<T> = {
        message: outcome.message ?? "OK",
        status,
        isSuccess: true,
        notifications: null,
        data: outcome.data,
      };
      return HttpResponse.json(envelope, { status });
    }
    case "failure": {
      const envelope: ApiEnvelope<T> = {
        message: outcome.message,
        status: outcome.status,
        isSuccess: false,
        notifications: outcome.notifications ?? [],
        data: null,
      };
      return HttpResponse.json(envelope, { status: outcome.status });
    }
    case "failure-with-ok-status": {
      const envelope: ApiEnvelope<T> = {
        message: outcome.message,
        status: outcome.status ?? 400,
        isSuccess: false,
        notifications: outcome.notifications ?? [],
        data: null,
      };
      return HttpResponse.json(envelope, { status: 200 });
    }
    case "raw":
      return new HttpResponse(outcome.body, { status: outcome.status });
    case "network-error":
      return HttpResponse.error();
  }
}
