using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

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

    #region Mudanças que derrubam token (usam os métodos de DOMÍNIO, nunca SQL cru)

    /// <summary>Troca a senha do operador pelo método de domínio e grava (a versão de segurança sobe).</summary>
    public static Task ChangePasswordAsync(
        this PostgresContainerFixture fixture, Guid tenantId, Guid operatorId,
        string newPassword = "Nova1!Senha", bool dedicated = false) =>
        UpdateAsync(fixture, tenantId, operatorId, dedicated, op => op.UpdatePassword(new Password(newPassword)));

    /// <summary>Muda o perfil do operador pelo método de domínio e grava (a versão sobe se o perfil for diferente).</summary>
    public static Task ChangeRoleAsync(
        this PostgresContainerFixture fixture, Guid tenantId, Guid operatorId, ERole role, bool dedicated = false) =>
        UpdateAsync(fixture, tenantId, operatorId, dedicated, op => op.UpdateRole(role));

    /// <summary>Desativa o operador pelo método de domínio e grava (a versão sobe).</summary>
    public static Task DeactivateAsync(
        this PostgresContainerFixture fixture, Guid tenantId, Guid operatorId, bool dedicated = false) =>
        UpdateAsync(fixture, tenantId, operatorId, dedicated, op => op.Deactivate());

    /// <summary>Reativa o operador pelo método de domínio e grava (a versão NÃO volta atrás).</summary>
    public static Task ReactivateAsync(
        this PostgresContainerFixture fixture, Guid tenantId, Guid operatorId, bool dedicated = false) =>
        UpdateAsync(fixture, tenantId, operatorId, dedicated, op => op.Activate());

    private static async Task UpdateAsync(
        PostgresContainerFixture fixture, Guid tenantId, Guid operatorId, bool dedicated, Action<Operator> change)
    {
        await using var context = dedicated
            ? fixture.CreateDedicatedContext(tenantId)
            : fixture.CreateContext(tenantId);

        var op = await context.Operators.SingleAsync(x => x.Id == operatorId);
        change(op);
        await context.SaveChangesAsync();
    }

    #endregion
}
