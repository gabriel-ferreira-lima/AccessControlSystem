using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Migrator.Commands {

    /// <summary>
    /// Verbo <c>migrate</c>: atualiza o catálogo, registra o tenant inicial (se configurado) e atualiza
    /// todas as bases de tenant. Nenhuma etapa cria operador (FR-026).
    /// </summary>
    public class MigrateCommand {

        private readonly DbContextOptions<CatalogDbContext> _catalogOptions;
        private readonly InitialTenantRegistrar _registrar;
        private readonly MigrationOrchestrator _orchestrator;
        private readonly InitialTenantOptions _initialTenant;
        private readonly TextWriter _output;

        public MigrateCommand(
            DbContextOptions<CatalogDbContext> catalogOptions,
            InitialTenantRegistrar registrar,
            MigrationOrchestrator orchestrator,
            IOptions<InitialTenantOptions> initialTenant,
            TextWriter output) {
            _catalogOptions = catalogOptions;
            _registrar = registrar;
            _orchestrator = orchestrator;
            _initialTenant = initialTenant.Value;
            _output = output;
        }

        /// <summary>Devolve o código de saída: 0 (tudo certo) ou 1 (alguma base falhou). A trava esgotada lança.</summary>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default) {

            // 1. Estrutura do catálogo.
            await using (var catalog = new CatalogDbContext(_catalogOptions)) {
                await catalog.Database.MigrateAsync(cancellationToken);
            }
            _output.WriteLine("Catálogo atualizado.");

            // 2. Adoção de uma base legada: base e tenant inicial entram no catálogo antes dos lotes.
            if (_initialTenant.IsConfigured) {
                await using var catalog = new CatalogDbContext(_catalogOptions);
                await _registrar.RegisterAsync(catalog, _initialTenant, cancellationToken);
                _output.WriteLine($"Tenant inicial registrado no catálogo (base '{_initialTenant.DatabaseName}').");
            }

            // 3 e 4. Trava global e atualização das bases em lotes.
            var summary = await _orchestrator.RunAsync(cancellationToken);

            // 5. Resumo: nome da base, estado e motivo curto. Nunca connection string.
            WriteSummary(summary);

            return summary.Failed > 0 ? ExitCodes.SomeDatabaseFailed : ExitCodes.Ok;
        }

        private void WriteSummary(MigrationSummary summary) {
            _output.WriteLine("Resumo da atualização das bases:");
            _output.WriteLine($"  Bases atualizadas: {summary.Updated}");
            _output.WriteLine($"  Bases já na versão atual (ignoradas): {summary.AlreadyCurrent}");
            _output.WriteLine($"  Bases com falha: {summary.Failed}");

            foreach (var outcome in summary.Outcomes.Where(x => x.State == EMigrationStatus.Failed)) {
                _output.WriteLine($"  - {outcome.Name}: falhou ({outcome.Reason})");
            }
        }
    }
}
