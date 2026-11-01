using AccessControlSystem.Application.SharedContext.Tenancy;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

/// <summary>
/// <see cref="ITenantContext"/> de teste: já resolvido com um tenant fixo, ou nunca resolvido
/// (para provar a falha fechada).
/// </summary>
public sealed class FixedTenantContext : ITenantContext
{
    private readonly Guid? _tenantId;

    public FixedTenantContext(Guid? tenantId) => _tenantId = tenantId;

    public static FixedTenantContext Unresolved => new(null);

    public bool IsResolved => _tenantId.HasValue;

    public Guid TenantId => _tenantId
        ?? throw new InvalidOperationException("O tenant da requisição ainda não foi resolvido.");
}
