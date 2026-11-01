using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>
    /// Atualiza UMA base de tenant em processo (sem o <c>dotnet ef</c>): aplica a
    /// <see cref="SeedAdminPolicy"/>, roda as migrations com o usuário migrator e devolve a versão
    /// em que a base ficou. Usa um contexto SEM os interceptors de tenant: migrations não são dado
    /// de tenant.
    /// </summary>
    public class EfDatabaseMigrationRunner : IDatabaseMigrationRunner {

        private const int MaxErrorLength = 1000;

        public async Task<MigrationRunResult> RunAsync(
            string migrationConnectionString,
            CancellationToken cancellationToken,
            Guid? initialTenantId = null) {

            try {
                // Um tenant inicial vazio atribuiria as linhas legadas a um tenant que não existe.
                if (initialTenantId == Guid.Empty) {
                    throw new ArgumentException("O tenant inicial não pode ser vazio.", nameof(initialTenantId));
                }

                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseNpgsql(migrationConnectionString, npgsql => npgsql.MigrationsAssembly("AccessControlSystem.Api"))
                    .Options;

                await using var context = new AppDbContext(options, new NoTenantContext());

                // Mantém a conexão aberta durante todo o processo (a mesma sessão serve à política,
                // às migrations e, na adoção de uma base legada, ao tenant inicial).
                await context.Database.OpenConnectionAsync(cancellationToken);

                await SeedAdminPolicy.ApplyAsync(context, cancellationToken);

                // Base que já tinha dados antes do multi-tenant: a migration AddTenantIdToOperators lê
                // acs.initial_tenant_id (na MESMA sessão) e atribui as linhas legadas a esse tenant. Sem
                // ele, linhas legadas ficam sem tenant e a migration seguinte falha no NOT NULL.
                if (initialTenantId.HasValue) {
                    await context.Database.ExecuteSqlAsync(
                        $"SELECT set_config('acs.initial_tenant_id', {initialTenantId.Value.ToString()}, false)",
                        cancellationToken);
                }

                await context.Database.MigrateAsync(cancellationToken);

                var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
                return new MigrationRunResult(true, applied.LastOrDefault(), null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                return new MigrationRunResult(false, null, Sanitize(exception, migrationConnectionString));
            }
        }

        // O motivo da falha vai para o catálogo e para o log: nunca pode conter connection string
        // nem senha.
        private static string Sanitize(Exception exception, string connectionString) {
            var message = $"{exception.GetType().Name}: {exception.Message}";

            message = message.Replace(connectionString, "[connection string omitida]", StringComparison.Ordinal);

            try {
                var password = new NpgsqlConnectionStringBuilder(connectionString).Password;
                if (!string.IsNullOrEmpty(password)) {
                    message = message.Replace(password, "***", StringComparison.Ordinal);
                }
            }
            catch (ArgumentException) {
                // connection string malformada: já foi removida inteira acima
            }

            return message.Length <= MaxErrorLength ? message : message[..MaxErrorLength];
        }
    }
}
