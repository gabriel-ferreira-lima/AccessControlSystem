using AccessControlSystem.Infrastructure.Catalog.Models;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>Resultado de uma execução do <c>migrate</c>. Nunca contém connection string nem segredo.</summary>
    public sealed record MigrationSummary(int Updated, int AlreadyCurrent, IReadOnlyList<DatabaseOutcome> Outcomes) {

        public int Failed => Outcomes.Count(x => x.State == EMigrationStatus.Failed);
    }

    /// <summary>O que aconteceu com uma base nesta execução. <c>Reason</c> só existe na falha.</summary>
    public sealed record DatabaseOutcome(string Name, EMigrationStatus State, string? Reason);

    /// <summary>A trava global não foi obtida dentro do tempo configurado (código de saída 4).</summary>
    public sealed class MigratorLockTimeoutException : Exception {

        public MigratorLockTimeoutException(int timeoutSeconds)
            : base($"A trava global do catálogo não foi obtida em {timeoutSeconds}s: outra execução do migrate está em andamento.") {
        }
    }
}
