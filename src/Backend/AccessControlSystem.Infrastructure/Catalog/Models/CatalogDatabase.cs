using AccessControlSystem.Application.SharedContext.Tenancy;

namespace AccessControlSystem.Infrastructure.Catalog.Models {

    /// <summary>
    /// Modelo de persistência de <c>tenant_databases</c>: onde os dados de um ou mais tenants moram
    /// e em que versão da estrutura a base está. O catálogo pertence ao sistema de provisionamento,
    /// então isto não é uma entidade de domínio: não tem regra, só dados.
    /// </summary>
    public class CatalogDatabase {
        public Guid Id { get; set; }

        /// <summary>Referência da base (chave em <c>TenantDatabases:{name}</c> da configuração). Não é connection string.</summary>
        public string Name { get; set; } = string.Empty;

        public EDatabaseKind Kind { get; set; }

        /// <summary>Id da última migration aplicada. <c>null</c> até a primeira atualização.</summary>
        public string? SchemaVersion { get; set; }

        public EMigrationStatus MigrationStatus { get; set; } = EMigrationStatus.Pending;

        public DateTime? MigrationAttemptedAt { get; set; }

        /// <summary>Motivo curto da última falha, sem connection string nem segredo.</summary>
        public string? MigrationError { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
