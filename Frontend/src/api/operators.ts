import { apiRequest } from "../lib/api-client";
import type {
  ActiveFilter,
  CreatedOperator,
  OperatorList,
  OperatorListItem,
  Role,
  UpdatedOperator,
} from "../types/api";

export interface CreateOperatorInput {
  email: string;
  password: string;
  role: Role;
}

/** POST /v1/account/create — cadastra um novo operador. */
export function createOperator(input: CreateOperatorInput, signal?: AbortSignal) {
  return apiRequest<CreatedOperator>("/v1/account/create", {
    method: "POST",
    body: input,
    authenticated: true,
    signal,
  });
}

/**
 * Campos da atualizacao. Apenas os informados sao enviados;
 * a API exige ao menos um e valida cada regra no servidor.
 */
export interface UpdateOperatorInput {
  email?: string;
  password?: string;
  role?: Role;
}

/** PUT /v1/account/update/{id} — altera e-mail, senha ou perfil de um operador. */
export function updateOperator(id: string, input: UpdateOperatorInput, signal?: AbortSignal) {
  return apiRequest<UpdatedOperator>(`/v1/account/update/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: input,
    authenticated: true,
    signal,
  });
}

export interface ListOperatorsQuery {
  page?: number;
  size?: number;
  isActive?: ActiveFilter;
}

/** GET /v1/account/get-list — lista paginada de operadores (perfil Admin). */
export function listOperators(query: ListOperatorsQuery = {}, signal?: AbortSignal) {
  const params = new URLSearchParams();
  if (query.page !== undefined) params.set("Page", String(query.page));
  if (query.size !== undefined) params.set("Size", String(query.size));
  if (query.isActive !== undefined) params.set("IsActive", query.isActive);
  const queryString = params.toString();

  return apiRequest<OperatorList>(`/v1/account/get-list${queryString ? `?${queryString}` : ""}`, {
    method: "GET",
    authenticated: true,
    signal,
  });
}

/**
 * PUT /v1/account/deactivate/{id} — desativa um operador (exclusao logica,
 * mantida para auditoria). Quem pede a desativacao vem do token no backend.
 */
export function deactivateOperator(id: string, signal?: AbortSignal) {
  return apiRequest<OperatorListItem>(`/v1/account/deactivate/${encodeURIComponent(id)}`, {
    method: "PUT",
    authenticated: true,
    signal,
  });
}

/** PUT /v1/account/activate/{id} — reativa um operador desativado. */
export function activateOperator(id: string, signal?: AbortSignal) {
  return apiRequest<OperatorListItem>(`/v1/account/activate/${encodeURIComponent(id)}`, {
    method: "PUT",
    authenticated: true,
    signal,
  });
}
