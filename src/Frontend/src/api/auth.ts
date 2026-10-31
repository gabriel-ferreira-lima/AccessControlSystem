import { apiRequest } from "../lib/api-client";
import type { AuthenticatedOperator } from "../types/api";

export interface LoginInput {
  email: string;
  password: string;
}

/** POST /v1/account/login — gera o token de acesso do sistema. */
export function login(input: LoginInput, signal?: AbortSignal) {
  return apiRequest<AuthenticatedOperator>("/v1/account/login", {
    method: "POST",
    body: input,
    signal,
  });
}
