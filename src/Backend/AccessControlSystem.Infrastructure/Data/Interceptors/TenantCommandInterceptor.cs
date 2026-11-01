using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data;
using System.Data.Common;

namespace AccessControlSystem.Infrastructure.Data.Interceptors {

    /// <summary>
    /// Leituras fora de transação: antepõe ao comando o <c>set_config(..., true)</c> do tenant, no
    /// mesmo lote (uma só ida ao banco). Sem transação explícita, o lote roda numa transação
    /// implícita, então o valor não sobrevive a ele e é compatível com PgBouncer em modo transação.
    /// Depois da execução, o leitor avança o primeiro resultado (o do <c>set_config</c>) para o EF
    /// ler apenas o resultado da consulta dele.
    /// Comandos que não são consulta e não estão numa transação falham fechados: sem tenant no banco,
    /// o RLS afetaria 0 linhas em silêncio.
    /// Instância única e sem estado: o tenant vem do contexto em uso.
    /// </summary>
    public sealed class TenantCommandInterceptor : DbCommandInterceptor {

        private const string TenantParameterName = "__acs_tenant";
        private const string SetConfigPrefix = "SELECT set_config('app.tenant_id', @__acs_tenant, true);\n";

        #region Leituras (consultas)

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result) {
            PrependTenant(command, eventData);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default) {
            PrependTenant(command, eventData);
            return new ValueTask<InterceptionResult<DbDataReader>>(result);
        }

        public override DbDataReader ReaderExecuted(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result) {
            if (HasTenantPrefix(command)) {
                result.NextResult();   // pula o resultado do set_config
            }

            return result;
        }

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default) {
            if (HasTenantPrefix(command)) {
                await result.NextResultAsync(cancellationToken);   // pula o resultado do set_config
            }

            return result;
        }

        #endregion

        #region Comandos que não são consulta (falham fechados fora de transação)

        public override InterceptionResult<int> NonQueryExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result) {
            EnsureInsideTransaction(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default) {
            EnsureInsideTransaction(command);
            return new ValueTask<InterceptionResult<int>>(result);
        }

        public override InterceptionResult<object> ScalarExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result) {
            EnsureInsideTransaction(command);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
            CancellationToken cancellationToken = default) {
            EnsureInsideTransaction(command);
            return new ValueTask<InterceptionResult<object>>(result);
        }

        #endregion

        private static void PrependTenant(DbCommand command, CommandEventData eventData) {
            // Dentro de uma transação do EF, o TenantTransactionInterceptor já informou o tenant.
            if (command.Transaction is not null || HasTenantPrefix(command)) {
                return;
            }

            var tenantId = TenantInterceptorSupport.GetTenantId(eventData.Context);

            // O valor vai sempre por parâmetro, nunca concatenado ao SQL.
            var parameter = command.CreateParameter();
            parameter.ParameterName = TenantParameterName;
            parameter.DbType = DbType.String;
            parameter.Value = tenantId.ToString();
            command.Parameters.Add(parameter);

            command.CommandText = SetConfigPrefix + command.CommandText;
        }

        private static bool HasTenantPrefix(DbCommand command) =>
            command.CommandText.StartsWith(SetConfigPrefix, StringComparison.Ordinal);

        private static void EnsureInsideTransaction(DbCommand command) {
            if (command.Transaction is null) {
                throw new InvalidOperationException(
                    "Comando fora de transação não é permitido no contexto com tenant: use SaveChanges " +
                    "ou uma transação explícita (Database.BeginTransaction), para o tenant ser informado ao banco.");
            }
        }
    }
}
