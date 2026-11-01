using AccessControlSystem.Infrastructure.Tenancy.Options;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Infrastructure.Tenancy {

    /// <summary>
    /// Entrega a connection string da base de um tenant a partir de <c>TenantDatabases:{name}</c>.
    /// O catálogo guarda só o nome da base; o segredo vem da configuração.
    /// </summary>
    public class TenantDatabaseConnectionProvider {

        private readonly TenantDatabasesOptions _options;

        public TenantDatabaseConnectionProvider(IOptions<TenantDatabasesOptions> options) {
            _options = options.Value;
        }

        /// <summary>
        /// Connection string (papel da aplicação) da base. Lança se não houver, e a mensagem cita só o
        /// nome da base: nunca uma connection string, a de migração ou o conteúdo de outra base.
        /// </summary>
        public string GetConnectionString(string databaseName) {
            if (_options.TryGetValue(databaseName, out var entry) && !string.IsNullOrWhiteSpace(entry.ConnectionString)) {
                return entry.ConnectionString;
            }

            throw new InvalidOperationException(
                $"A base '{databaseName}' não tem connection string configurada (TenantDatabases:{databaseName}:ConnectionString).");
        }
    }
}
