using AccessControlSystem.Application.SharedContext.Tenancy;

namespace AccessControlSystem.Infrastructure.Tenancy {

    /// <summary>
    /// Tenant da requisição em curso (escopo do contêiner de DI). É preenchido uma única vez pelo
    /// <see cref="TenantResolver"/> e depois só lido; não existe cache nem estado compartilhado
    /// entre requisições.
    /// </summary>
    public sealed class TenantScope : ITenantContext {

        private Guid _tenantId;
        private string? _databaseName;

        public bool IsResolved => _databaseName is not null;

        public Guid TenantId =>
            IsResolved ? _tenantId : throw NotResolved();

        /// <summary>Nome da base do tenant no catálogo (chave de <c>TenantDatabases</c>).</summary>
        public string DatabaseName =>
            _databaseName ?? throw NotResolved();

        public void Set(Guid tenantId, string databaseName) {
            if (tenantId == Guid.Empty) {
                throw new ArgumentException("O tenant não pode ser vazio.", nameof(tenantId));
            }

            if (string.IsNullOrWhiteSpace(databaseName)) {
                throw new ArgumentException("A base do tenant precisa de um nome.", nameof(databaseName));
            }

            // Um escopo atende um único tenant: trocar no meio da requisição misturaria dados de tenants.
            if (IsResolved) {
                throw new InvalidOperationException("O tenant desta requisição já foi resolvido e não pode ser trocado.");
            }

            _tenantId = tenantId;
            _databaseName = databaseName;
        }

        private static InvalidOperationException NotResolved() =>
            new("O tenant ainda não foi resolvido nesta requisição.");
    }
}
