import type { ApiEnvelope, ApiNotification } from "../types/api";
import { readSession } from "./session-storage";

const RAW_BASE = import.meta.env.VITE_API_BASE_URL ?? "https://localhost:7219";

/** URL base da API, sem barra final. */
export const API_BASE_URL = RAW_BASE.replace(/\/+$/, "");

const NETWORK_ERROR =
  "Nao foi possivel se comunicar com o servidor. Verifique a conexao e tente novamente.";
const UNEXPECTED_ERROR = "Ocorreu um erro inesperado. Tente novamente em instantes.";

/**
 * Erro normalizado a partir do envelope de resposta da API.
 * `message` e `notifications` vem diretamente do backend e sao exibidos como recebidos.
 */
export class ApiError extends Error {
  readonly status: number;
  readonly notifications: ApiNotification[];

  constructor(message: string, status: number, notifications: ApiNotification[] = []) {
    super(message);
    this.name = "ApiError";
    this.status = status;
    this.notifications = notifications;
  }
}

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  authenticated?: boolean;
  signal?: AbortSignal;
}

export function isAbortError(error: unknown): boolean {
  return error instanceof DOMException && error.name === "AbortError";
}

type UnauthorizedHandler = () => void;

let unauthorizedHandler: UnauthorizedHandler | null = null;

/**
 * Registrado pelo AuthProvider. Toda chamada autenticada que volta 401
 * dispara este handler — o servidor decidiu que o token nao vale mais
 * (conta desativada, expirado, etc.); o cliente nao tenta adivinhar o
 * motivo, so encerra a sessao local e deixa a navegacao normal levar de
 * volta ao login.
 */
export function setUnauthorizedHandler(handler: UnauthorizedHandler | null): void {
  unauthorizedHandler = handler;
}

export async function apiRequest<T>(
  path: string,
  options: RequestOptions = {},
): Promise<ApiEnvelope<T>> {
  const { method = "GET", body, authenticated = false, signal } = options;

  const headers: Record<string, string> = { Accept: "application/json" };
  if (body !== undefined) {
    headers["Content-Type"] = "application/json";
  }
  if (authenticated) {
    const token = readSession()?.token;
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }
  }

  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    });
  } catch (error) {
    if (isAbortError(error)) throw error;
    throw new ApiError(NETWORK_ERROR, 0);
  }

  const rawText = await response.text();
  let envelope: ApiEnvelope<T> | null = null;
  if (rawText.length > 0) {
    try {
      envelope = JSON.parse(rawText) as ApiEnvelope<T>;
    } catch {
      envelope = null;
    }
  }

  const failed = !response.ok || envelope?.isSuccess === false;

  if (authenticated && response.status === 401) {
    unauthorizedHandler?.();
  }

  if (failed) {
    throw new ApiError(
      envelope?.message?.trim() || UNEXPECTED_ERROR,
      envelope?.status ?? response.status,
      envelope?.notifications ?? [],
    );
  }

  return (
    envelope ?? {
      message: "",
      status: response.status,
      isSuccess: true,
      notifications: null,
      data: null,
    }
  );
}
