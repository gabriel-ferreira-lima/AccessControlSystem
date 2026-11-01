using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace AccessControlSystem.Infrastructure.Data.Interceptors {

    /// <summary>
    /// Informa o tenant ao banco no início de toda transação do EF, com <c>set_config(..., true)</c>:
    /// o valor vale só até o fim da transação, então é compatível com PgBouncer em modo transação.
    /// Cobre o <c>SaveChanges</c> (o contexto usa <c>AutoTransactionBehavior.Always</c>) e qualquer
    /// transação explícita aberta com <c>Database.BeginTransaction</c>.
    /// Instância única e sem estado: o tenant vem do contexto em uso.
    /// </summary>
    public sealed class TenantTransactionInterceptor : DbTransactionInterceptor {

        private const string SetConfigSql = "SELECT set_config('app.tenant_id', @tenant, true)";

        // Falha fechada antes de abrir a transação: sem tenant resolvido, nem o BEGIN é enviado.
        public override InterceptionResult<DbTransaction> TransactionStarting(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result) {
            TenantInterceptorSupport.GetTenantId(eventData.Context);
            return result;
        }

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection, TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default) {
            TenantInterceptorSupport.GetTenantId(eventData.Context);
            return new ValueTask<InterceptionResult<DbTransaction>>(result);
        }

        public override DbTransaction TransactionStarted(
            DbConnection connection, TransactionEndEventData eventData, DbTransaction result) {
            var tenantId = TenantInterceptorSupport.GetTenantId(eventData.Context);

            try {
                using var command = CreateSetConfigCommand(connection, result, tenantId);
                command.ExecuteScalar();
            }
            catch {
                result.Dispose();   // desfaz a transação recém-aberta: nunca segue sem o tenant
                throw;
            }

            return result;
        }

        public override async ValueTask<DbTransaction> TransactionStartedAsync(
            DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
            CancellationToken cancellationToken = default) {
            var tenantId = TenantInterceptorSupport.GetTenantId(eventData.Context);

            try {
                await using var command = CreateSetConfigCommand(connection, result, tenantId);
                await command.ExecuteScalarAsync(cancellationToken);
            }
            catch {
                await result.DisposeAsync();
                throw;
            }

            return result;
        }

        private static DbCommand CreateSetConfigCommand(DbConnection connection, DbTransaction transaction, Guid tenantId) {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = SetConfigSql;

            // O valor vai sempre por parâmetro, nunca concatenado ao SQL.
            var parameter = command.CreateParameter();
            parameter.ParameterName = "tenant";
            parameter.Value = tenantId.ToString();
            command.Parameters.Add(parameter);

            return command;
        }
    }
}
