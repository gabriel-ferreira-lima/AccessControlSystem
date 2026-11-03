using System.Collections.Concurrent;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Migrator.Commands {

    /// <summary>
    /// Verbo <c>verify-no-seed-admin</c> (recusado em Development): confere o catálogo e TODAS as bases de tenant do
    /// ambiente e reprova se alguma tiver o administrador fixo da migration <c>SeedAdmin</c> (FR-031 a FR-035). É uma
    /// barreira independente da <c>SeedAdminPolicy</c>, que só impede base NOVA de recebê-lo: esta pega também base
    /// antiga ou alterada à mão. Só lê.
    /// </summary>
    /// <remarks>
    /// Percorre o catálogo em lotes paginados por chave (<c>Migrator:BatchSize</c>), com concorrência limitada
    /// (<c>Migrator:MaxDegreeOfParallelism</c>); uma base com erro não interrompe as outras. A saída traz a contagem e,
    /// nas falhas, o NOME LÓGICO da base e um motivo curto: nunca connection string, e-mail, identificador de operador
    /// nem senha. Códigos: 0 aprovou; 6 recusado em Development; 7 achou o administrador fixo; 8 não conseguiu
    /// verificar (base ou catálogo inacessível, lista das bases ilegível, ou catálogo e configuração divergem). Com 7 e 8
    /// juntos, sai 7 e lista os dois. Se a lista das bases falhar, o catálogo conta como não verificado, as bases já
    /// verificadas ficam no relatório e a divergência com a configuração NÃO é conferida (a lista estaria incompleta).
    /// </remarks>
    public class VerifyNoSeedAdminCommand {

        private readonly IHostEnvironment _environment;
        private readonly CatalogRegistry _registry;
        private readonly string _catalogConnectionString;
        private readonly TenantDatabasesOptions _databases;
        private readonly MigratorOptions _options;
        private readonly TextWriter _output;

        public VerifyNoSeedAdminCommand(
            IHostEnvironment environment,
            CatalogRegistry registry,
            IOptions<CatalogMigrationOptions> catalog,
            IOptions<TenantDatabasesOptions> databases,
            IOptions<MigratorOptions> options,
            TextWriter output) {
            _environment = environment;
            _registry = registry;
            _catalogConnectionString = catalog.Value.CatalogMigration;
            _databases = databases.Value;
            _options = options.Value;
            _output = output;
        }

        /// <summary>Devolve o código de saída: 0, 6 (Development), 7 (achou) ou 8 (não conseguiu verificar).</summary>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default) {

            // A recusa vem antes de qualquer outra coisa: nada é lido nem consultado.
            if (_environment.IsDevelopment()) {
                _output.WriteLine(DevelopmentRefusalMessage);
                return ExitCodes.VerifyRefusedInDevelopment;
            }

            // Os dados fixos saem da própria migration. Se ela mudou de forma e a leitura falha, NÃO se aprova nada.
            SeedAdminFingerprint fingerprint;
            try {
                fingerprint = SeedAdminFingerprint.Current;
            }
            catch (InvalidOperationException exception) {
                _output.WriteLine($"verify-no-seed-admin reprovado: não foi possível identificar o administrador fixo na migration SeedAdmin ({exception.Message}).");
                return ExitCodes.CannotVerify;
            }

            var verifier = new FixedAdminVerifier(fingerprint);
            var results = new ConcurrentBag<DatabaseVerification>();
            var registered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 1. O catálogo.
            var catalog = await verifier.CheckCatalogAsync(_catalogConnectionString, cancellationToken);

            // 2. Todas as bases cadastradas no catálogo, em lotes paginados por chave e com concorrência limitada.
            // Se a LISTA das bases falhar (catálogo fora do ar, antes ou no meio da paginação), não se aprova nada: o
            // catálogo conta como não verificado e as bases já verificadas continuam no relatório.
            DatabaseVerification? listingFailure = null;
            Guid? after = null;
            while (true) {
                IReadOnlyList<CatalogDatabase> page;

                try {
                    page = await _registry.ListPageAsync(after, _options.BatchSize, cancellationToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException) {
                    // Só o TIPO da exceção: a mensagem poderia trazer servidor, porta ou connection string.
                    listingFailure = new DatabaseVerification(FixedAdminVerifier.CatalogName, DatabaseVerdict.Unverifiable,
                        $"a lista das bases cadastradas não pôde ser lida ({exception.GetType().Name})");
                    break;
                }

                if (page.Count == 0) {
                    break;
                }

                foreach (var database in page) {
                    registered.Add(database.Name);
                }

                await Parallel.ForEachAsync(
                    page,
                    new ParallelOptions {
                        MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism,
                        CancellationToken = cancellationToken
                    },
                    async (database, token) => results.Add(await CheckAsync(verifier, database.Name, token)));

                after = page[^1].Id;

                if (page.Count < _options.BatchSize) {
                    break;
                }
            }

            // 3. Divergência: configurada, mas ausente do catálogo. Não dá para dizer que o ambiente está limpo. Só roda com a
            // lista COMPLETA: se a leitura falhou, "registered" está incompleto e acusaria falsamente toda base configurada.
            if (listingFailure is null) {
                foreach (var (name, entry) in _databases) {
                    var configured = !string.IsNullOrWhiteSpace(entry.MigrationConnectionString)
                        || !string.IsNullOrWhiteSpace(entry.ConnectionString);

                    if (configured && !registered.Contains(name)) {
                        results.Add(new DatabaseVerification(name, DatabaseVerdict.Unverifiable,
                            "configurada em TenantDatabases, mas não cadastrada no catálogo"));
                    }
                }
            }

            return Report(catalog, results.OrderBy(x => x.Name, StringComparer.Ordinal).ToList(), registered.Count, listingFailure);
        }

        private async Task<DatabaseVerification> CheckAsync(FixedAdminVerifier verifier, string name, CancellationToken cancellationToken) {
            // A connection string de migração (o migrator, com BYPASSRLS) é a única que enxerga todos os tenants.
            if (!_databases.TryGetValue(name, out var entry) || string.IsNullOrWhiteSpace(entry.MigrationConnectionString)) {
                return new DatabaseVerification(name, DatabaseVerdict.Unverifiable,
                    $"cadastrada no catálogo, sem a connection string de migração configurada (TenantDatabases:{name}:MigrationConnectionString)");
            }

            return await verifier.CheckTenantDatabaseAsync(name, entry.MigrationConnectionString, cancellationToken);
        }

        private int Report(
            DatabaseVerification catalog, IReadOnlyList<DatabaseVerification> tenants, int registeredCount,
            DatabaseVerification? listingFailure) {
            var all = tenants.Append(catalog).ToList();

            // A falha da lista vira UMA linha do catálogo: se ele já saiu como não verificado, essa linha basta.
            if (listingFailure is not null && catalog.Verdict != DatabaseVerdict.Unverifiable) {
                all.Add(listingFailure);
            }

            var contaminated = all.Where(x => x.Verdict == DatabaseVerdict.Contaminated).ToList();
            var unverifiable = all.Where(x => x.Verdict == DatabaseVerdict.Unverifiable).ToList();

            var verified = tenants.Count(x => x.Verdict != DatabaseVerdict.Unverifiable);
            if (listingFailure is null) {
                _output.WriteLine($"Bases de tenant verificadas: {verified} de {registeredCount}. Catálogo: {Describe(catalog)}.");
            }
            else {
                // Sem a lista não existe o "de N": só se conta o que chegou a ser verificado antes da falha.
                var catalogState = catalog.Verdict == DatabaseVerdict.Contaminated ? Describe(catalog) : "não verificado";
                _output.WriteLine($"Bases de tenant verificadas antes da falha: {verified}. Catálogo: {catalogState}.");
            }

            foreach (var item in contaminated) {
                _output.WriteLine($"  - {item.Name}: contém o administrador fixo ({item.Reason})");
            }

            foreach (var item in unverifiable) {
                _output.WriteLine($"  - {item.Name}: não foi possível verificar ({item.Reason})");
            }

            if (contaminated.Count > 0) {
                _output.WriteLine("verify-no-seed-admin REPROVADO: o administrador fixo da migration SeedAdmin foi encontrado.");
                return ExitCodes.FixedAdminFound;
            }

            if (unverifiable.Count > 0) {
                _output.WriteLine("verify-no-seed-admin REPROVADO: nem tudo pôde ser verificado (uma base ou o catálogo não foi conferido).");
                return ExitCodes.CannotVerify;
            }

            _output.WriteLine("verify-no-seed-admin aprovado: nenhuma base contém o administrador fixo.");
            return ExitCodes.Ok;
        }

        private static string Describe(DatabaseVerification catalog) => catalog.Verdict switch {
            DatabaseVerdict.Clean => "verificado",
            DatabaseVerdict.Contaminated => "contaminado",
            _ => "não verificado"
        };

        /// <summary>Mensagem da recusa, também usada pela guarda do <see cref="MigratorApplication"/> (antes da configuração).</summary>
        public const string DevelopmentRefusalMessage =
            "verify-no-seed-admin recusado: o ambiente é Development, onde o administrador fixo da migration de desenvolvimento existe de propósito. Nada foi verificado.";
    }
}
