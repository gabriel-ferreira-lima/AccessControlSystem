namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>Atualiza a estrutura de UMA base de tenant. O orquestrador chama uma vez por base.</summary>
    public interface IDatabaseMigrationRunner {

        /// <param name="migrationConnectionString">Connection string do usuário com DDL (migrator).</param>
        /// <param name="initialTenantId">
        /// Tenant que passa a ser dono das linhas legadas (sem <c>tenant_id</c>) desta base.
        /// Só se informa na base que já tinha dados antes do multi-tenant.
        /// </param>
        Task<MigrationRunResult> RunAsync(
            string migrationConnectionString,
            CancellationToken cancellationToken,
            Guid? initialTenantId = null);
    }

    /// <summary>Resultado da atualização de uma base. <c>Error</c> nunca contém connection string nem segredo.</summary>
    public sealed record MigrationRunResult(bool Succeeded, string? SchemaVersion, string? Error);
}
