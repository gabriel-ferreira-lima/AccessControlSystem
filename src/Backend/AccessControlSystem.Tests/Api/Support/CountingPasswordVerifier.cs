using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate;

namespace AccessControlSystem.Tests.Api.Support;

/// <summary>
/// Decorator do verificador de senha real que conta as chamadas (seguro para uso concorrente). Prova que, numa rajada
/// simultânea contra a mesma conta, só as tentativas admitidas chegam ao cálculo do hash.
/// </summary>
public sealed class CountingPasswordVerifier : IPasswordVerifier
{
    private readonly PasswordVerifier _inner = new();
    private int _calls;

    /// <summary>Quantas verificações de hash foram executadas.</summary>
    public int Calls => Volatile.Read(ref _calls);

    public bool Verify(Operator? @operator, string password)
    {
        Interlocked.Increment(ref _calls);
        return _inner.Verify(@operator, password);
    }
}
