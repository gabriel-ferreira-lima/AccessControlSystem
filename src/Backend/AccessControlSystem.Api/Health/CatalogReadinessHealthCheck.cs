using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AccessControlSystem.Api.Health {

    /// <summary>
    /// Verificação "pronta" (<c>/health/ready</c>): o catálogo responde? É anônima e consulta o catálogo, que também é
    /// consultado a cada requisição autenticada; sem proteção, uma inundação anônima esgotaria o pool de conexões.
    /// </summary>
    /// <remarks>
    /// Proteção contra abuso: o resultado fica em cache por <see cref="CacheTtl"/> e só UMA consulta fica em voo, então a
    /// Api faz no máximo 1 consulta ao catálogo a cada 5 s por instância, qualquer que seja o volume de chamadas. O TTL
    /// vale para sucesso e para falha (a queda e a volta do catálogo aparecem em até 5 s). O cache guarda só um booleano
    /// e um instante, e é POR INSTÂNCIA de propósito: readiness é por instância e não leva dado de tenant.
    /// </remarks>
    public class CatalogReadinessHealthCheck : IHealthCheck {

        /// <summary>Validade do resultado em cache (5 s: curto o bastante para a volta do catálogo aparecer logo).</summary>
        public static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);

        /// <summary>Tempo limite da consulta ao catálogo (2 s), em tempo real.</summary>
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<CatalogReadinessHealthCheck> _logger;

        // Uma única consulta em voo: as chamadas concorrentes esperam aqui e leem o resultado novo.
        private readonly SemaphoreSlim _gate = new(1, 1);

        private volatile Snapshot? _snapshot;

        public CatalogReadinessHealthCheck(
            IServiceScopeFactory scopeFactory, TimeProvider timeProvider, ILogger<CatalogReadinessHealthCheck> logger) {
            _scopeFactory = scopeFactory;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default) {
            if (TryReadCache(out var cached)) {
                return Result(cached);
            }

            await _gate.WaitAsync(cancellationToken);
            try {
                // Quem esperou o semáforo encontra o resultado que a chamada anterior acabou de guardar.
                if (TryReadCache(out cached)) {
                    return Result(cached);
                }

                var healthy = await ProbeAsync(cancellationToken);
                _snapshot = new Snapshot(healthy, _timeProvider.GetUtcNow() + CacheTtl);
                return Result(healthy);
            } finally {
                _gate.Release();
            }
        }

        private bool TryReadCache(out bool healthy) {
            var snapshot = _snapshot;
            if (snapshot is not null && _timeProvider.GetUtcNow() < snapshot.ValidUntil) {
                healthy = snapshot.Healthy;
                return true;
            }

            healthy = false;
            return false;
        }

        private async Task<bool> ProbeAsync(CancellationToken requestAborted) {
            // O tempo limite corre em tempo real (o timer do TimeProvider base é o do sistema).
            using var timeout = new CancellationTokenSource(Timeout, _timeProvider);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, timeout.Token);

            try {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var probe = scope.ServiceProvider.GetRequiredService<ICatalogProbe>();
                await probe.ProbeAsync(linked.Token);
                return true;
            } catch (OperationCanceledException) when (requestAborted.IsCancellationRequested) {
                // Foi o cliente que desistiu: não é falha do catálogo e nada fica em cache.
                throw;
            } catch (Exception exception) {
                // Só o TIPO da exceção vai ao log: a mensagem poderia trazer servidor, porta ou connection string.
                _logger.LogWarning("Verificação pronta indisponível: o catálogo não respondeu ({ExceptionType}).",
                    exception.GetType().Name);
                return false;
            }
        }

        private static HealthCheckResult Result(bool healthy) =>
            healthy ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();

        private sealed record Snapshot(bool Healthy, DateTimeOffset ValidUntil);
    }
}
