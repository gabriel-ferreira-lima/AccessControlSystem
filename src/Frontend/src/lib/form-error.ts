import { ApiError } from "./api-client";
import type { ApiNotification } from "../types/api";

export { isAbortError } from "./api-client";

/**
 * Erro pronto para exibir num <Alert>: message + notifications, exatamente
 * como a API devolveu (algumas regras do backend so aparecem em notifications,
 * nunca na message de topo — ver Specification.Ensure no backend).
 */
export interface FormError {
  message: string;
  notifications: ApiNotification[];
}

export const NO_ERROR: FormError = { message: "", notifications: [] };

export const UNEXPECTED_ERROR: FormError = {
  message: "Ocorreu um erro inesperado. Tente novamente.",
  notifications: [],
};

/** Normaliza qualquer erro de uma chamada de API para o formato do <Alert>. */
export function toFormError(error: unknown): FormError {
  if (error instanceof ApiError) {
    return { message: error.message, notifications: error.notifications };
  }
  return UNEXPECTED_ERROR;
}
