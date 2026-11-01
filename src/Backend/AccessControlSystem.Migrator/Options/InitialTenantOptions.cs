using AccessControlSystem.Application.SharedContext.Tenancy;

namespace AccessControlSystem.Migrator.Options {

    /// <summary>
    /// Tenant inicial (seção <c>InitialTenant</c>): o tenant que passa a ser dono dos dados de uma base
    /// que já existia antes do multi-tenant. Os valores vêm sempre da configuração, nunca do código.
    /// O CNPJ é gravado como informado, sem validação: o catálogo pertence ao provisionamento e só a
    /// unicidade é imposta pelo banco.
    /// </summary>
    public class InitialTenantOptions {

        public const string SectionName = "InitialTenant";

        public Guid Id { get; set; }

        public string Cnpj { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        /// <summary>Nome da base que já tem os dados atuais (chave de <c>TenantDatabases:{name}</c>).</summary>
        public string DatabaseName { get; set; } = string.Empty;

        public EDatabaseKind DatabaseKind { get; set; } = EDatabaseKind.Shared;
    }
}
