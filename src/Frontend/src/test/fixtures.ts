import type {
  ApiNotification,
  OperatorList,
  OperatorListItem,
  Session,
} from "../types/api";

// Dados sintéticos e determinísticos (sem aleatoriedade), no domínio reservado @example.test.
let sequence = 0;

function nextSequence(): number {
  sequence += 1;
  return sequence;
}

function guid(value: number): string {
  return `00000000-0000-4000-8000-${String(value).padStart(12, "0")}`;
}

export function buildSession(overrides: Partial<Session> = {}): Session {
  const number = nextSequence();
  return {
    token: "token-de-teste",
    id: guid(number),
    email: `operador-${number}@example.test`,
    role: "Operator",
    ...overrides,
  };
}

export const adminSession: Session = {
  token: "token-de-teste-admin",
  id: guid(900001),
  email: "admin@example.test",
  role: "Admin",
};

export const operatorSession: Session = {
  token: "token-de-teste-operador",
  id: guid(900002),
  email: "operador@example.test",
  role: "Operator",
};

export function buildOperator(overrides: Partial<OperatorListItem> = {}): OperatorListItem {
  const number = nextSequence();
  return {
    id: guid(number),
    email: `usuario-${number}@example.test`,
    role: "Operator",
    isActive: true,
    ...overrides,
  };
}

/** Página coerente: `total`, `totalPages`, `page` e `size` calculados a partir da lista. */
export function buildOperatorList(
  operators: OperatorListItem[],
  overrides: Partial<OperatorList> = {},
): OperatorList {
  const size = overrides.size ?? 10;
  const total = overrides.total ?? operators.length;
  return {
    operators,
    page: 1,
    size,
    total,
    totalPages: total === 0 ? 0 : Math.ceil(total / size),
    ...overrides,
  };
}

export function buildNotification(key: string, message: string): ApiNotification {
  return { key, message };
}
