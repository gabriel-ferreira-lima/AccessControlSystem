using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>
    /// Registro, no catálogo, do estado de atualização de cada base (FR-023): única fonte da verdade
    /// sobre "em que versão cada base está". Cada operação usa o seu próprio contexto, porque o
    /// orquestrador atualiza várias bases em paralelo. As transições são feitas aqui, não no modelo.
    /// </summary>
    public class CatalogRegistry {

        private readonly DbContextOptions<CatalogDbContext> _options;

        public CatalogRegistry(DbContextOptions<CatalogDbContext> options) {
            _options = options;
        }

        public async Task MarkRunningAsync(Guid databaseId, CancellationToken cancellationToken = default) {
            var now = DateTime.UtcNow;

            await UpdateAsync(databaseId, set => set
                .SetProperty(x => x.MigrationStatus, EMigrationStatus.Running)
                .SetProperty(x => x.MigrationAttemptedAt, now)
                .SetProperty(x => x.MigrationError, (string?)null), cancellationToken);
        }

        public async Task MarkSucceededAsync(Guid databaseId, string schemaVersion, CancellationToken cancellationToken = default) {
            await UpdateAsync(databaseId, set => set
                .SetProperty(x => x.MigrationStatus, EMigrationStatus.Succeeded)
                .SetProperty(x => x.SchemaVersion, schemaVersion)
                .SetProperty(x => x.MigrationError, (string?)null), cancellationToken);
        }

        /// <summary>
        /// Grava a falha e o motivo (truncado em 1000 caracteres; quem chama já tirou connection string e
        /// segredo). A versão registrada continua sendo a última que de fato foi aplicada.
        /// </summary>
        public async Task MarkFailedAsync(Guid databaseId, string reason, CancellationToken cancellationToken = default) {
            var truncated = reason.Length <= ErrorSanitizer.MaxLength ? reason : reason[..ErrorSanitizer.MaxLength];

            await UpdateAsync(databaseId, set => set
                .SetProperty(x => x.MigrationStatus, EMigrationStatus.Failed)
                .SetProperty(x => x.MigrationError, truncated), cancellationToken);
        }

        /// <summary>
        /// Próximo lote de bases a atualizar, por chave (<c>id &gt; afterId</c>): as que não estão na última
        /// migration ou cuja situação não é <c>Succeeded</c>. Servido por <c>ix_tenant_databases_migration_status_id</c>.
        /// </summary>
        public virtual async Task<IReadOnlyList<CatalogDatabase>> ListEligibleAsync(
            Guid? afterId, int size, string latestMigrationId, CancellationToken cancellationToken = default) {

            await using var context = new CatalogDbContext(_options);

            var query = context.Databases
                .AsNoTracking()
                .Where(x => x.SchemaVersion == null
                    || x.SchemaVersion != latestMigrationId
                    || x.MigrationStatus != EMigrationStatus.Succeeded);

            if (afterId.HasValue) {
                var after = afterId.Value;
                query = query.Where(x => x.Id.CompareTo(after) > 0);
            }

            return await query
                .OrderBy(x => x.Id)
                .Take(size)
                .ToListAsync(cancellationToken);
        }

        public async Task<int> CountAsync(CancellationToken cancellationToken = default) {
            await using var context = new CatalogDbContext(_options);
            return await context.Databases.CountAsync(cancellationToken);
        }

        private async Task UpdateAsync(
            Guid databaseId,
            Action<Microsoft.EntityFrameworkCore.Query.UpdateSettersBuilder<CatalogDatabase>> setters,
            CancellationToken cancellationToken) {

            await using var context = new CatalogDbContext(_options);

            var rows = await context.Databases
                .Where(x => x.Id == databaseId)
                .ExecuteUpdateAsync(setters, cancellationToken);

            if (rows == 0) {
                throw new InvalidOperationException($"A base {databaseId} não está cadastrada no catálogo.");
            }
        }
    }
}
