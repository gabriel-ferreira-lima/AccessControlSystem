using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate {

    /// <summary>
    /// Limite de login por conta, EM MEMÓRIA da instância (violação temporária dos Princípios I e XIII, registrada em
    /// Complexity Tracking; a spec 06 troca por Redis no mesmo contrato).
    /// <para>
    /// A admissão é atômica: <b>um único <c>lock</c></b> cobre a consulta do bloqueio, a contagem e a decisão de bloquear.
    /// É isso que fecha a corrida de uma rajada simultânea: só as 5 primeiras tentativas por nível recebem <c>null</c>.
    /// </para>
    /// <para>
    /// Privacidade: a chave é o SHA-256 de <c>"{tenantId}:{email}"</c>; o e-mail nunca fica em memória nem em log.
    /// Estado só em campos de instância (singleton): nada estático (<c>NoStaticTenantStateTests</c>).
    /// </para>
    /// <para>
    /// Falha fechada (FR-022): com o registro cheio, varre expirados e remove um lote de contas NÃO bloqueadas; nunca remove
    /// conta bloqueada. Se ainda não couber, a conta nova recebe um bloqueio de 60 s, sem ser contada. Jamais libera sem limite.
    /// </para>
    /// </summary>
    public class InMemoryLoginThrottle : ILoginThrottle {

        // Constantes (não são estado): espera de uma conta que não pôde ser rastreada e intervalo mínimo entre varreduras.
        private const int UntrackedWaitSeconds = 60;
        private const int SweepIntervalSeconds = 60;

        private sealed class Entry {
            public int Failures;
            public int Level;
            public DateTimeOffset? BlockedUntil;
            public DateTimeOffset LastEventAt;
        }

        private readonly TimeProvider _timeProvider;
        private readonly ILogger<InMemoryLoginThrottle> _logger;
        private readonly int _maxFailures;
        private readonly double _baseWaitSeconds;
        private readonly double _maxWaitSeconds;
        private readonly TimeSpan _idleExpiry;
        private readonly int _maxTrackedAccounts;

        private readonly object _gate = new();
        private readonly Dictionary<string, Entry> _entries = new();
        private DateTimeOffset _lastSweep;

        public InMemoryLoginThrottle(TimeProvider timeProvider, IOptions<LoginThrottleOptions> options, ILogger<InMemoryLoginThrottle> logger) {
            var value = options.Value;

            _timeProvider = timeProvider;
            _logger = logger;
            _maxFailures = value.MaxFailures;
            _baseWaitSeconds = value.BaseWaitSeconds;
            _maxWaitSeconds = value.MaxWaitSeconds;
            _idleExpiry = TimeSpan.FromSeconds(value.IdleExpirySeconds);
            _maxTrackedAccounts = value.MaxTrackedAccounts;
            _lastSweep = timeProvider.GetUtcNow();
        }

        public Task<TimeSpan?> TryAcquireAttemptAsync(Guid tenantId, string email, CancellationToken cancellationToken = default) {
            var key = AccountKey(tenantId, email);
            var now = _timeProvider.GetUtcNow();

            TimeSpan? result;
            int blockedLevel = 0;
            TimeSpan blockedWait = TimeSpan.Zero;

            lock (_gate) {
                SweepIfDue(now);

                if (_entries.TryGetValue(key, out var entry) && IsExpired(entry, now)) {
                    _entries.Remove(key);   // FR-032: sem tentativa nem bloqueio por 15 min, recomeça do zero
                    entry = null;
                }

                if (entry is null) {
                    if (_entries.Count >= _maxTrackedAccounts) {
                        MakeRoom(now);

                        if (_entries.Count >= _maxTrackedAccounts) {
                            // Cheio de contas bloqueadas: falha fechada. Não rastreia nem conta; nunca devolve null.
                            return Task.FromResult<TimeSpan?>(TimeSpan.FromSeconds(UntrackedWaitSeconds));
                        }
                    }

                    entry = new Entry();
                    _entries[key] = entry;
                }
                else if (entry.BlockedUntil is { } blockedUntil) {
                    if (blockedUntil > now) {
                        // Bloqueada: devolve o tempo restante e NÃO conta nada.
                        return Task.FromResult<TimeSpan?>(blockedUntil - now);
                    }

                    entry.BlockedUntil = null;   // a espera acabou
                }

                // Tentativa admitida: conta como falha provisória (o login com sucesso zera, por ResetAsync).
                entry.Failures++;
                entry.LastEventAt = now;

                if (entry.Failures >= _maxFailures) {
                    entry.Level++;
                    blockedWait = WaitFor(entry.Level);
                    entry.BlockedUntil = now + blockedWait;
                    entry.Failures = 0;
                    entry.LastEventAt = entry.BlockedUntil.Value;   // o descarte conta a partir do FIM do bloqueio
                    blockedLevel = entry.Level;
                }

                result = null;   // esta tentativa segue (inclusive a que acabou de atingir o máximo)
            }

            if (blockedLevel > 0) {
                // Só quando o bloqueio é CRIADO (não nas tentativas barradas). Sem e-mail, senha ou token: só o tenant e
                // os 8 primeiros caracteres do hash da conta.
                _logger.LogWarning(
                    "Login: conta bloqueada por excesso de tentativas. Tenant {TenantId}, conta {AccountHash}, nível {Level}, espera {WaitSeconds}s",
                    tenantId, key[..8], blockedLevel, (int)blockedWait.TotalSeconds);
            }

            return Task.FromResult(result);
        }

        public Task ResetAsync(Guid tenantId, string email, CancellationToken cancellationToken = default) {
            var key = AccountKey(tenantId, email);

            lock (_gate) {
                _entries.Remove(key);
            }

            return Task.CompletedTask;
        }

        /// <summary>Espera do nível: <c>min(base × 2^(nível−1), teto)</c>.</summary>
        private TimeSpan WaitFor(int level) {
            var seconds = _baseWaitSeconds * Math.Pow(2, Math.Min(level - 1, 30));
            return TimeSpan.FromSeconds(Math.Min(seconds, _maxWaitSeconds));
        }

        private bool IsExpired(Entry entry, DateTimeOffset now) => now - entry.LastEventAt >= _idleExpiry;

        private static bool IsBlocked(Entry entry, DateTimeOffset now) => entry.BlockedUntil is { } until && until > now;

        /// <summary>Varredura de expirados, no máximo uma por minuto (chamada dentro do lock).</summary>
        private void SweepIfDue(DateTimeOffset now) {
            if (now - _lastSweep < TimeSpan.FromSeconds(SweepIntervalSeconds)) {
                return;
            }

            RemoveExpired(now);
            _lastSweep = now;
        }

        private void RemoveExpired(DateTimeOffset now) {
            var expired = _entries.Where(x => IsExpired(x.Value, now)).Select(x => x.Key).ToList();

            foreach (var key in expired) {
                _entries.Remove(key);
            }
        }

        /// <summary>
        /// Registro cheio: remove expirados e, se preciso, um lote das contas NÃO bloqueadas mais antigas (elas só têm contagem
        /// parcial). NUNCA remove conta bloqueada, senão inundar com e-mails falsos expulsaria bloqueios reais.
        /// </summary>
        private void MakeRoom(DateTimeOffset now) {
            RemoveExpired(now);

            if (_entries.Count < _maxTrackedAccounts) {
                return;
            }

            var batch = Math.Max(1, _maxTrackedAccounts / 10);
            var victims = _entries
                .Where(x => !IsBlocked(x.Value, now))
                .OrderBy(x => x.Value.LastEventAt)
                .Take(batch)
                .Select(x => x.Key)
                .ToList();

            foreach (var key in victims) {
                _entries.Remove(key);
            }
        }

        private static string AccountKey(Guid tenantId, string email) {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId}:{email}"));
            return Convert.ToHexStringLower(hash);
        }
    }
}
