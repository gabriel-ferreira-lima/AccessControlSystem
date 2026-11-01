using AccessControlSystem.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// O TenantScope é o tenant da requisição: preenchido uma vez e só lido depois.
public class TenantScopeTests
{
    private static readonly Guid TenantId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");

    [Fact]
    public void Set_resolve_o_contexto()
    {
        var scope = new TenantScope();

        scope.Set(TenantId, "shared_01");

        Assert.True(scope.IsResolved);
        Assert.Equal(TenantId, scope.TenantId);
        Assert.Equal("shared_01", scope.DatabaseName);
    }

    [Fact]
    public void Set_duas_vezes_lanca()
    {
        var scope = new TenantScope();
        scope.Set(TenantId, "shared_01");

        Assert.Throws<InvalidOperationException>(() => scope.Set(Guid.NewGuid(), "dedicated_01"));

        // O tenant original continua intacto: a segunda chamada não troca nada.
        Assert.Equal(TenantId, scope.TenantId);
        Assert.Equal("shared_01", scope.DatabaseName);
    }

    [Fact]
    public void TenantId_lanca_quando_nao_resolvido()
    {
        var scope = new TenantScope();

        Assert.False(scope.IsResolved);
        Assert.Throws<InvalidOperationException>(() => scope.TenantId);
        Assert.Throws<InvalidOperationException>(() => scope.DatabaseName);
    }

    [Fact]
    public void Set_recusa_tenant_vazio_e_base_sem_nome()
    {
        var scope = new TenantScope();

        Assert.Throws<ArgumentException>(() => scope.Set(Guid.Empty, "shared_01"));
        Assert.Throws<ArgumentException>(() => scope.Set(TenantId, " "));
        Assert.False(scope.IsResolved);
    }
}
