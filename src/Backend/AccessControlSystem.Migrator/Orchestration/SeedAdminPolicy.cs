using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Reflection;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>
    /// Impede que uma base NOVA receba o administrador de e-mail e senha fixos (FR-030/FR-031).
    /// A migration <c>SeedAdmin</c> já foi aplicada em bases antigas e não pode ser editada, então,
    /// quando a base não tem nenhuma migration aplicada, a política grava no histórico a linha dela
    /// como já aplicada e o <c>Migrate</c> seguinte a pula. Bases que já têm histórico não são tocadas.
    /// </summary>
    public static class SeedAdminPolicy {

        public const string SeedAdminMigrationId = "20260908171522_SeedAdmin";

        public static async Task ApplyAsync(DbContext context, CancellationToken cancellationToken = default) {
            var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);

            if (applied.Any()) {
                return;   // base com histórico: nada a fazer (idempotente)
            }

            var history = context.GetService<IHistoryRepository>();

            await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), cancellationToken);
            await context.Database.ExecuteSqlRawAsync(
                history.GetInsertScript(new HistoryRow(SeedAdminMigrationId, EfProductVersion())),
                cancellationToken);
        }

        // Mesma versão que o EF grava no histórico (informational version sem o sufixo "+commit").
        private static string EfProductVersion() =>
            typeof(DbContext).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
                .InformationalVersion.Split('+')[0];
    }
}
