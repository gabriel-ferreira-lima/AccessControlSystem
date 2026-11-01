using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Tests.Infrastructure.Fixtures;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

/// <summary>Dados de teste por tenant. Cada teste usa o seu próprio tenant (<c>NewTenantId()</c>).</summary>
public static class TenantSeed
{
    public static string UniqueEmail(string prefix = "tenant") => $"{prefix}-{Guid.NewGuid():N}@test.com";

    public static Operator NewOperator(string? email = null, ERole role = ERole.Operator, bool isActive = true)
    {
        var op = new Operator(new Email(email ?? UniqueEmail()), new Password("Str0ng!Pass"), role);
        if (!isActive) op.Deactivate();
        return op;
    }

    /// <summary>Grava um operador no tenant informado, na base compartilhada (ou na dedicada).</summary>
    public static async Task<Operator> SeedOperatorAsync(
        this PostgresContainerFixture fixture,
        Guid tenantId,
        string? email = null,
        ERole role = ERole.Operator,
        bool isActive = true,
        bool dedicated = false)
    {
        var op = NewOperator(email, role, isActive);

        await using var context = dedicated
            ? fixture.CreateDedicatedContext(tenantId)
            : fixture.CreateContext(tenantId);
        context.Operators.Add(op);
        await context.SaveChangesAsync();

        return op;
    }
}
