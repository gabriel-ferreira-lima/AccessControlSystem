using AccessControlSystem.Application.SharedContext.Tenancy;

namespace AccessControlSystem.Infrastructure.Tenancy {

    /// <summary>
    /// Resolve o tenant da requisição: consulta o catálogo, recusa tenant inexistente ou inativo e
    /// preenche o <see cref="TenantScope"/> com o tenant e o nome da sua base. Consulta o catálogo a
    /// cada chamada (sem cache), então inativar um tenant vale na requisição seguinte.
    /// </summary>
    public class TenantResolver : ITenantResolver {

        private readonly ITenantCatalog _catalog;
        private readonly TenantDatabaseConnectionProvider _connectionProvider;
        private readonly TenantScope _scope;

        public TenantResolver(
            ITenantCatalog catalog,
            TenantDatabaseConnectionProvider connectionProvider,
            TenantScope scope) {
            _catalog = catalog;
            _connectionProvider = connectionProvider;
            _scope = scope;
        }

        public async Task<bool> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default) {
            var tenant = await _catalog.GetTenantByIdAsync(tenantId, cancellationToken);

            if (tenant is null || !tenant.IsActive) {
                return false;
            }

            // Falha fechada: tenant ativo cuja base não está configurada é erro de operação (500),
            // não "tenant inexistente" e jamais uma base escolhida por aproximação.
            _connectionProvider.GetConnectionString(tenant.Database.Name);

            _scope.Set(tenant.Id, tenant.Database.Name);
            return true;
        }
    }
}
