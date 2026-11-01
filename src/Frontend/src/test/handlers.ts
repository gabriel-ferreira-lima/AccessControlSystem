import { http } from "msw";
import type { HttpHandler } from "msw";
import type {
  ActiveFilter,
  AuthenticatedOperator,
  CreatedOperator,
  Me,
  OperatorList,
  OperatorListItem,
  UpdatedMe,
  UpdatedOperator,
} from "../types/api";
import { toResponse } from "./envelope";
import type { Outcome } from "./envelope";
import { apiUrl } from "./server";

/** O que a API simulada recebeu; passado ao espião `onRequest` de cada fábrica. */
export interface ReceivedRequest {
  method: string;
  url: string;
  authorization: string | null;
  /** Corpo interpretado como JSON; `undefined` quando vazio. */
  body: unknown;
}

export type OnRequest = (received: ReceivedRequest) => void;

async function receive(request: Request): Promise<ReceivedRequest> {
  const text = await request.text();
  let body: unknown;
  if (text.length > 0) {
    try {
      body = JSON.parse(text);
    } catch {
      body = text;
    }
  }
  return {
    method: request.method,
    url: request.url,
    authorization: request.headers.get("Authorization"),
    body,
  };
}

/** POST /v1/account/login */
export function loginHandler(
  outcome: Outcome<AuthenticatedOperator>,
  onRequest?: OnRequest,
): HttpHandler {
  return http.post(apiUrl("/v1/account/login"), async ({ request }) => {
    onRequest?.(await receive(request));
    return toResponse(outcome);
  });
}

/** POST /v1/account/create */
export function createOperatorHandler(
  outcome: Outcome<CreatedOperator>,
  onRequest?: OnRequest,
): HttpHandler {
  return http.post(apiUrl("/v1/account/create"), async ({ request }) => {
    onRequest?.(await receive(request));
    return toResponse(outcome);
  });
}

/** PUT /v1/account/update/:id */
export function updateOperatorHandler(
  outcome: Outcome<UpdatedOperator>,
  onRequest?: OnRequest,
): HttpHandler {
  return http.put(apiUrl("/v1/account/update/:id"), async ({ request }) => {
    onRequest?.(await receive(request));
    return toResponse(outcome);
  });
}

/** Parâmetros de consulta do get-list, já convertidos. */
export interface ListOperatorsQuery {
  page: number;
  size: number;
  isActive: ActiveFilter;
}

/**
 * GET /v1/account/get-list. Aceita um resultado fixo ou uma função que responde conforme a
 * consulta (`Page`, `Size`, `IsActive`), para testar paginação e filtros.
 */
export function listOperatorsHandler(
  outcome: Outcome<OperatorList> | ((query: ListOperatorsQuery) => Outcome<OperatorList>),
  onRequest?: OnRequest,
): HttpHandler {
  return http.get(apiUrl("/v1/account/get-list"), async ({ request }) => {
    onRequest?.(await receive(request));
    if (typeof outcome !== "function") {
      return toResponse(outcome);
    }
    const params = new URL(request.url).searchParams;
    return toResponse(
      outcome({
        page: Number(params.get("Page")),
        size: Number(params.get("Size")),
        isActive: params.get("IsActive") as ActiveFilter,
      }),
    );
  });
}

/** PUT /v1/account/activate/:id */
export function activateOperatorHandler(
  outcome: Outcome<OperatorListItem>,
  onRequest?: OnRequest,
): HttpHandler {
  return http.put(apiUrl("/v1/account/activate/:id"), async ({ request }) => {
    onRequest?.(await receive(request));
    return toResponse(outcome);
  });
}

/** PUT /v1/account/deactivate/:id */
export function deactivateOperatorHandler(
  outcome: Outcome<OperatorListItem>,
  onRequest?: OnRequest,
): HttpHandler {
  return http.put(apiUrl("/v1/account/deactivate/:id"), async ({ request }) => {
    onRequest?.(await receive(request));
    return toResponse(outcome);
  });
}

/** GET /v1/account/get-me */
export function getMeHandler(outcome: Outcome<Me>, onRequest?: OnRequest): HttpHandler {
  return http.get(apiUrl("/v1/account/get-me"), async ({ request }) => {
    onRequest?.(await receive(request));
    return toResponse(outcome);
  });
}

/** PUT /v1/account/update-me */
export function updateMeHandler(outcome: Outcome<UpdatedMe>, onRequest?: OnRequest): HttpHandler {
  return http.put(apiUrl("/v1/account/update-me"), async ({ request }) => {
    onRequest?.(await receive(request));
    return toResponse(outcome);
  });
}
