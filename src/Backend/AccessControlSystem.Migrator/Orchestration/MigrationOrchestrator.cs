using System.Collections.Concurrent;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using AccessControlSystem.Migrator.Options;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>
    /// Atualiza todas as bases do catálogo em lotes, com concorrência limitada, retomável e com a
    /// falha de uma base sem impedir as demais (research R-07). O estado de cada base fica no catálogo,
    /// então uma nova execução só refaz o que falta.
    /// </summary>
    public class MigrationOrchestrator {

        /// <summary>Chave da trava global (<c>pg_advisory_lock</c>) no banco do catálogo.</summary>
        public const long GlobalLockKey = 0x4143535F4D494752;   // "ACS_MIGR"

        private readonly CatalogRegistry _registry;
        private readonly IDatabaseMigrationRunner _runner;
        private readonly TenantDatabasesOptions _databases;
        private readonly MigratorOptions _options;
        private readonly string _catalogConnectionString;
        private readonly InitialTenantOptions _initialTenant;

        public MigrationOrchestrator(
            CatalogRegistry registry,
            IDatabaseMigrationRunner runner,
            IOptions<TenantDatabasesOptions> databases,
            IOptions<MigratorOptions> options,
            IOptions<CatalogMigrationOptions> catalog,
            IOptions<InitialTenantOptions> initialTenant) {
            _registry = registry;
            _runner = runner;
            _databases = databases.Value;
            _options = options.Value;
            _catalogConnectionString = catalog.Value.CatalogMigration;
            _initialTenant = initialTenant.Value;
        }

        public async Task<MigrationSummary> RunAsync(CancellationToken cancellationToken = default) {
            var latest = AppMigrations.LatestId();

            // Uma execução por vez no catálogo todo. A trava dura até o fim e é solta ao sair.
            await using var globalLock = await GlobalLock.AcquireAsync(
                _catalogConnectionString, _options.LockTimeoutSeconds, cancellationToken);

            var outcomes = new ConcurrentBag<DatabaseOutcome>();
            Guid? after = null;

            while (true) {
                var batch = await _registry.ListEligibleAsync(after, _options.BatchSize, latest, cancellationToken);

                if (batch.Count == 0) {
                    break;
                }

                await Parallel.ForEachAsync(
                    batch,
                    new ParallelOptions {
                        MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism,
                        CancellationToken = cancellationToken
                    },
                    async (database, token) => outcomes.Add(await MigrateOneAsync(database, latest, token)));

                // A paginação é por chave: a base que falhou não volta a aparecer nesta execução.
                after = batch[^1].Id;

                if (batch.Count < _options.BatchSize) {
                    break;
                }
            }

            var processed = outcomes.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            var total = await _registry.CountAsync(cancellationToken);

            return new MigrationSummary(
                Updated: processed.Count(x => x.State == EMigrationStatus.Succeeded),
                AlreadyCurrent: Math.Max(0, total - processed.Count),
                Outcomes: processed);
        }

        private async Task<DatabaseOutcome> MigrateOneAsync(CatalogDatabase database, string latest, CancellationToken cancellationToken) {
            await _registry.MarkRunningAsync(database.Id, cancellationToken);

            var connectionString = _databases.TryGetValue(database.Name, out var entry)
                ? entry.MigrationConnectionString
                : null;

            if (string.IsNullOrWhiteSpace(connectionString)) {
                return await FailAsync(
                    database,
                    $"A base '{database.Name}' não tem a connection string de migração configurada (TenantDatabases:{database.Name}:MigrationConnectionString).",
                    cancellationToken);
            }

            // Só a base que já tinha os dados anteriores ao multi-tenant recebe o tenant inicial.
            Guid? initialTenantId =
                _initialTenant.IsConfigured
                && string.Equals(_initialTenant.DatabaseName, database.Name, StringComparison.OrdinalIgnoreCase)
                    ? _initialTenant.Id
                    : null;

            MigrationRunResult result;
            try {
                result = await _runner.RunAsync(connectionString, cancellationToken, initialTenantId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                result = new MigrationRunResult(false, null, ErrorSanitizer.FromException(exception, connectionString));
            }

            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.SchemaVersion)) {
                await _registry.MarkSucceededAsync(database.Id, result.SchemaVersion, cancellationToken);
                return new DatabaseOutcome(database.Name, EMigrationStatus.Succeeded, null);
            }

            var reason = result.Succeeded
                ? "O executor não informou a versão da estrutura aplicada."
                : result.Error ?? "Falha sem detalhe.";

            // Defesa em profundidade: o executor já limpa o motivo, mas o que vai para o catálogo passa
            // sempre por aqui.
            return await FailAsync(database, ErrorSanitizer.Sanitize(reason, connectionString), cancellationToken);
        }

        private async Task<DatabaseOutcome> FailAsync(CatalogDatabase database, string reason, CancellationToken cancellationToken) {
            await _registry.MarkFailedAsync(database.Id, reason, cancellationToken);
            return new DatabaseOutcome(
                database.Name,
                EMigrationStatus.Failed,
                reason.Length <= ErrorSanitizer.MaxLength ? reason : reason[..ErrorSanitizer.MaxLength]);
        }

        /// <summary>Trava global: conexão dedicada ao catálogo que segura o <c>pg_advisory_lock</c> até ser descartada.</summary>
        private sealed class GlobalLock : IAsyncDisposable {

            private readonly NpgsqlConnection _connection;

            private GlobalLock(NpgsqlConnection connection) {
                _connection = connection;
            }

            public static async Task<GlobalLock> AcquireAsync(
                string catalogConnectionString, int timeoutSeconds, CancellationToken cancellationToken) {

                var connection = new NpgsqlConnection(catalogConnectionString);

                try {
                    await connection.OpenAsync(cancellationToken);

                    // lock_timeout vale também para a espera por advisory locks: estourado, o PostgreSQL
                    // devolve 55P03 e a execução termina com o código 4.
                    await using (var timeout = new NpgsqlCommand("SELECT set_config('lock_timeout', @timeout, false)", connection)) {
                        timeout.Parameters.AddWithValue("timeout", $"{(long)timeoutSeconds * 1000}ms");
                        await timeout.ExecuteNonQueryAsync(cancellationToken);
                    }

                    await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", connection)) {
                        acquire.Parameters.AddWithValue("key", GlobalLockKey);
                        await acquire.ExecuteNonQueryAsync(cancellationToken);
                    }

                    return new GlobalLock(connection);
                }
                catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.LockNotAvailable) {
                    await connection.DisposeAsync();
                    throw new MigratorLockTimeoutException(timeoutSeconds);
                }
                catch {
                    await connection.DisposeAsync();
                    throw;
                }
            }

            public async ValueTask DisposeAsync() {
                try {
                    await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", _connection);
                    release.Parameters.AddWithValue("key", GlobalLockKey);
                    await release.ExecuteNonQueryAsync();
                }
                catch (NpgsqlException) {
                    // conexão já caída: o PostgreSQL solta a trava junto com a sessão
                }
                finally {
                    await _connection.DisposeAsync();
                }
            }
        }
    }
}
