import { apiRequest } from "../lib/api-client";
import type { Me, UpdatedMe } from "../types/api";

/**
 * GET /v1/account/get-me — dados atuais da propria conta, somente leitura.
 * Nao existe forma de editar por esta rota; alteracoes vao por updateMe.
 */
export function getMe(signal?: AbortSignal) {
  return apiRequest<Me>("/v1/account/get-me", {
    method: "GET",
    authenticated: true,
    signal,
  });
}

export interface UpdateMeInput {
  email?: string;
  password?: string;
}

/**
 * PUT /v1/account/update-me — altera e-mail e/ou senha da propria conta.
 * O servidor identifica a conta pelo token; nenhum identificador e enviado.
 */
export function updateMe(input: UpdateMeInput, signal?: AbortSignal) {
  return apiRequest<UpdatedMe>("/v1/account/update-me", {
    method: "PUT",
    body: input,
    authenticated: true,
    signal,
  });
}
