using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Tenancy;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Tests.Application.SharedContext.Tenancy;

// O resolvedor consulta o catálogo a cada chamada (sem cache) e preenche o TenantScope da requisição.
public class TenantResolverTests
{
    private static readonly Guid TenantId = Guid.Parse("d1d1d1d1-0000-4000-8000-000000000001");

    private readonly ITenantCatalog _catalog = Substitute.For<ITenantCatalog>();
    private readonly TenantScope _scope = new();
    private readonly TenantResolver _resolver;

    public TenantResolverTests()
    {
        var provider = new TenantDatabaseConnectionProvider(Options.Create(new TenantDatabasesOptions
        {
            ["shared_01"] = new TenantDatabaseOptions { ConnectionString = "Host=db;Database=shared_01" }
        }));

        _resolver = new TenantResolver(_catalog, provider, _scope);
    }

    private void CatalogReturns(bool isActive, string databaseName = "shared_01") =>
        _catalog.GetTenantByIdAsync(TenantId, Arg.Any<CancellationToken>())
            .Returns(new TenantInfo(TenantId, "12345678000190", "Empresa", isActive,
                new TenantDatabaseInfo(databaseName, EDatabaseKind.Shared)));

    [Fact]
    public async Task Resolve_tenant_ativo()
    {
        CatalogReturns(isActive: true);

        var resolved = await _resolver.ResolveAsync(TenantId);

        Assert.True(resolved);
        Assert.True(_scope.IsResolved);
        Assert.Equal(TenantId, _scope.TenantId);
        Assert.Equal("shared_01", _scope.DatabaseName);
    }

    [Fact]
    public async Task Devolve_false_para_tenant_inexistente()
    {
        _catalog.GetTenantByIdAsync(TenantId, Arg.Any<CancellationToken>()).Returns((TenantInfo?)null);

        var resolved = await _resolver.ResolveAsync(TenantId);

        Assert.False(resolved);
        Assert.False(_scope.IsResolved);
    }

    [Fact]
    public async Task Devolve_false_para_tenant_inativo()
    {
        CatalogReturns(isActive: false);

        var resolved = await _resolver.ResolveAsync(TenantId);

        Assert.False(resolved);
        Assert.False(_scope.IsResolved);
    }

    [Fact]
    public async Task Lanca_quando_a_base_nao_tem_connection_string()
    {
        CatalogReturns(isActive: true, databaseName: "base_sem_configuracao");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _resolver.ResolveAsync(TenantId));

        Assert.False(_scope.IsResolved);   // falha fechada: nada fica resolvido pela metade
    }

    [Fact]
    public async Task Resolver_duas_vezes_no_mesmo_escopo_lanca()
    {
        CatalogReturns(isActive: true);
        await _resolver.ResolveAsync(TenantId);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _resolver.ResolveAsync(TenantId));
    }

    [Fact]
    public async Task Consulta_o_catalogo_a_cada_chamada_sem_cache()
    {
        CatalogReturns(isActive: false);

        await _resolver.ResolveAsync(TenantId);
        await _resolver.ResolveAsync(TenantId);

        await _catalog.Received(2).GetTenantByIdAsync(TenantId, Arg.Any<CancellationToken>());
    }
}
