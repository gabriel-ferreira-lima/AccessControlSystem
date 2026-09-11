// Contratos da API do Sistema de Controle de Acesso.
// Estes tipos descrevem apenas a forma das respostas; nenhuma regra
// de validacao ou de seguranca e replicada no cliente.

export type Role = "Admin" | "Operator";

export interface ApiNotification {
  key: string;
  message: string;
}

export interface ApiEnvelope<T> {
  message: string;
  status: number;
  isSuccess: boolean;
  notifications: ApiNotification[] | null;
  data: T | null;
}

/** Retorno de POST /v1/account/login */
export interface AuthenticatedOperator {
  token: string;
  id: string;
  email: string;
  role: Role;
}

/** Retorno de POST /v1/account/create */
export interface CreatedOperator {
  id: string;
  email: string;
  role: Role;
}

/** Retorno de PUT /v1/account/update/{id} */
export interface UpdatedOperator {
  id: string;
  email: string;
  role: Role;
}

/** Retorno de PUT /v1/account/update-me */
export interface UpdatedMe {
  id: string;
  email: string;
}

/** Retorno de GET /v1/account/get-me — somente leitura */
export interface Me {
  id: string;
  email: string;
  role: Role;
}

/** Tambem e a forma de retorno de DELETE /delete/{id} e PUT /activate/{id}. */
export interface OperatorListItem {
  id: string;
  email: string;
  role: Role;
  isActive: boolean;
}

/** Filtro de status aceito por GET /v1/account/get-list (query "IsActive"). */
export type ActiveFilter = "Active" | "Inactive" | "All";

/** Retorno de GET /v1/account/get-list (paginado) */
export interface OperatorList {
  operators: OperatorListItem[];
  page: number;
  size: number;
  total: number;
  totalPages: number;
}

/** Sessao mantida localmente enquanto a aba estiver aberta. */
export type Session = AuthenticatedOperator;
