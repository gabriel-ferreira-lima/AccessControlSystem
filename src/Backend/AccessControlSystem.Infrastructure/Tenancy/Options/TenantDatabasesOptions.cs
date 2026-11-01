namespace AccessControlSystem.Infrastructure.Tenancy.Options {

    /// <summary>
    /// Connection strings das bases de tenant, por nome da base no catálogo (seção
    /// <c>TenantDatabases</c>): <c>TenantDatabases:{name}:ConnectionString</c> (papel da aplicação) e,
    /// só para o Migrator, <c>MigrationConnectionString</c> (papel com DDL). O catálogo guarda só o
    /// nome; o segredo vem de variável de ambiente ou do gerenciador de segredos.
    /// </summary>
    public class TenantDatabasesOptions : Dictionary<string, TenantDatabaseOptions> {

        public const string SectionName = "TenantDatabases";

        public TenantDatabasesOptions() : base(StringComparer.OrdinalIgnoreCase) {
        }
    }

    public class TenantDatabaseOptions {

        /// <summary>Connection string do papel da aplicação (acs_app), sujeita ao RLS.</summary>
        public string? ConnectionString { get; set; }

        /// <summary>Connection string do papel com DDL (acs_migrator). Só o Migrator a usa; a Api nunca a recebe.</summary>
        public string? MigrationConnectionString { get; set; }
    }
}
