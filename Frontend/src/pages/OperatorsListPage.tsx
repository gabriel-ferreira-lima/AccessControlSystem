import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { activateOperator, deactivateOperator, listOperators } from "../api/operators";
import { isAbortError, toFormError } from "../lib/form-error";
import type { FormError } from "../lib/form-error";
import { roleLabel } from "../lib/labels";
import { Alert } from "../components/ui/Alert";
import { Button } from "../components/ui/Button";
import { ConfirmDialog } from "../components/ui/ConfirmDialog";
import type { ActiveFilter, OperatorList, OperatorListItem } from "../types/api";

const PAGE_SIZES = [10, 25, 50, 100];

interface PendingAction {
  operator: OperatorListItem;
  type: "activate" | "deactivate";
}

export function OperatorsListPage() {
  const [page, setPage] = useState(1);
  const [size, setSize] = useState(10);
  const [statusFilter, setStatusFilter] = useState<ActiveFilter>("Active");
  const [reloadKey, setReloadKey] = useState(0);

  const [data, setData] = useState<OperatorList | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<FormError | null>(null);

  const [pendingAction, setPendingAction] = useState<PendingAction | null>(null);
  const [actionSubmitting, setActionSubmitting] = useState(false);
  const [actionError, setActionError] = useState<FormError | null>(null);
  const actionAbortRef = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    let active = true;

    setLoading(true);
    setError(null);

    listOperators({ page, size, isActive: statusFilter }, controller.signal)
      .then((response) => {
        if (active && response.data) {
          setData(response.data);
        }
      })
      .catch((err: unknown) => {
        if (!active) return;
        if (isAbortError(err)) return;
        setError(toFormError(err));
      })
      .finally(() => {
        if (active) setLoading(false);
      });

    return () => {
      active = false;
      controller.abort();
    };
  }, [page, size, statusFilter, reloadKey]);

  useEffect(() => () => actionAbortRef.current?.abort(), []);

  const totalPages = data?.totalPages ?? 0;
  const currentPage = data?.page ?? page;
  const operators = data?.operators ?? [];

  function openConfirm(operator: OperatorListItem, type: PendingAction["type"]) {
    setActionError(null);
    setPendingAction({ operator, type });
  }

  function closeConfirm() {
    if (actionSubmitting) return;
    setPendingAction(null);
    setActionError(null);
  }

  async function confirmPendingAction() {
    if (!pendingAction) return;

    setActionSubmitting(true);
    setActionError(null);
    actionAbortRef.current?.abort();
    const controller = new AbortController();
    actionAbortRef.current = controller;

    try {
      const call = pendingAction.type === "deactivate" ? deactivateOperator : activateOperator;
      await call(pendingAction.operator.id, controller.signal);
      setPendingAction(null);
      setReloadKey((key) => key + 1);
    } catch (err) {
      if (isAbortError(err)) return;
      setActionError(toFormError(err));
    } finally {
      setActionSubmitting(false);
    }
  }

  return (
    <div className="page">
      <header className="page-header page-header--row">
        <div>
          <h1 className="page-header__title">Operadores</h1>
          <p className="page-header__description">
            Contas de operador cadastradas no sistema.
          </p>
        </div>
        <div className="page-header__actions">
          <label className="toolbar-select">
            <span>Mostrar</span>
            <select
              className="inline-select"
              value={statusFilter}
              onChange={(event) => {
                setStatusFilter(event.target.value as ActiveFilter);
                setPage(1);
              }}
              disabled={loading}
            >
              <option value="All">Todos</option>
              <option value="Active">Ativos</option>
              <option value="Inactive">Inativos</option>
            </select>
          </label>
          <Button
            variant="ghost"
            size="sm"
            onClick={() => setReloadKey((key) => key + 1)}
            disabled={loading}
          >
            Atualizar
          </Button>
          <Link to="/operadores/novo" className="btn btn--primary btn--sm">
            Criar operador
          </Link>
        </div>
      </header>

      <section className="panel">
        {error ? (
          <Alert tone="error" title={error.message} notifications={error.notifications} />
        ) : loading && !data ? (
          <p className="empty-note">Carregando operadores...</p>
        ) : operators.length === 0 ? (
          <p className="empty-note">Nenhum operador encontrado para este filtro.</p>
        ) : (
          <>
            <div className="table-scroll" aria-busy={loading || undefined}>
              <table className="data-table">
                <thead>
                  <tr>
                    <th scope="col">E-mail</th>
                    <th scope="col">Perfil</th>
                    <th scope="col">Status</th>
                    <th scope="col">
                      <span className="sr-only">Acoes</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {operators.map((operator) => (
                    <tr key={operator.id}>
                      <td>{operator.email}</td>
                      <td>
                        <span className="tag">{roleLabel(operator.role)}</span>
                      </td>
                      <td>
                        <span className={`tag ${operator.isActive ? "tag--active" : "tag--inactive"}`}>
                          {operator.isActive ? "Ativo" : "Inativo"}
                        </span>
                      </td>
                      <td className="data-table__actions">
                        <Link
                          to={`/operadores/editar?id=${encodeURIComponent(operator.id)}`}
                          state={{ email: operator.email, role: operator.role }}
                          className="action-link"
                        >
                          Editar
                        </Link>
                        {operator.isActive ? (
                          <button
                            type="button"
                            className="action-link action-link--danger"
                            onClick={() => openConfirm(operator, "deactivate")}
                          >
                            Desativar
                          </button>
                        ) : (
                          <button
                            type="button"
                            className="action-link action-link--success"
                            onClick={() => openConfirm(operator, "activate")}
                          >
                            Ativar
                          </button>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="pager">
              <p className="pager__info">
                {data?.total ?? 0} operador(es) &middot; pagina {currentPage} de{" "}
                {Math.max(totalPages, 1)}
              </p>

              <div className="pager__controls">
                <label className="toolbar-select">
                  <span>Itens por pagina</span>
                  <select
                    className="inline-select"
                    value={size}
                    onChange={(event) => {
                      setSize(Number(event.target.value));
                      setPage(1);
                    }}
                    disabled={loading}
                  >
                    {PAGE_SIZES.map((value) => (
                      <option key={value} value={value}>
                        {value}
                      </option>
                    ))}
                  </select>
                </label>

                <div className="pager__nav">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setPage((value) => Math.max(1, value - 1))}
                    disabled={loading || currentPage <= 1}
                  >
                    Anterior
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setPage((value) => value + 1)}
                    disabled={loading || currentPage >= totalPages}
                  >
                    Proxima
                  </Button>
                </div>
              </div>
            </div>
          </>
        )}
      </section>

      <ConfirmDialog
        open={pendingAction !== null}
        title={pendingAction?.type === "deactivate" ? "Desativar operador?" : "Ativar operador?"}
        description={
          pendingAction ? (
            pendingAction.type === "deactivate" ? (
              <>
                Tem certeza que deseja desativar <strong>{pendingAction.operator.email}</strong>?
                A conta deixara de conseguir acessar o sistema.
              </>
            ) : (
              <>
                Tem certeza que deseja ativar <strong>{pendingAction.operator.email}</strong>?
              </>
            )
          ) : null
        }
        confirmLabel={pendingAction?.type === "deactivate" ? "Desativar" : "Ativar"}
        tone={pendingAction?.type === "deactivate" ? "danger" : "default"}
        pending={actionSubmitting}
        error={actionError}
        onConfirm={confirmPendingAction}
        onCancel={closeConfirm}
      />
    </div>
  );
}
