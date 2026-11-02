namespace AccessControlSystem.Tests.Support;

/// <summary>
/// Relógio controlável para testar bloqueio, dobra da espera, teto e descarte sem esperar de verdade (sem pacote novo:
/// o <see cref="TimeProvider"/> é da BCL). Começa no instante REAL da criação, para não distorcer nada que dependa de
/// "agora" (ex.: a validade de um token), e só anda quando o teste chama <see cref="Advance"/>.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    private readonly object _gate = new();
    private DateTimeOffset _now;

    public FakeTimeProvider() : this(DateTimeOffset.UtcNow)
    {
    }

    public FakeTimeProvider(DateTimeOffset start) => _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    /// <summary>Avança o relógio.</summary>
    public void Advance(TimeSpan amount)
    {
        lock (_gate) _now += amount;
    }
}
