using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Services;
using AccessControlSystem.Tests.Infrastructure.Fixtures;

namespace AccessControlSystem.Tests.Infrastructure.Catalog;

// O TenantCatalog devolve só record da Application (nunca o modelo de persistência) e lê pelo papel
// da aplicação (acs_app), que só tem SELECT no catálogo.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class TenantCatalogTests
{
    private readonly PostgresContainerFixture _fixture;

    public TenantCatalogTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static DateTime UtcNow()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }

    private async Task<(CatalogDatabase Database, CatalogTenant Tenant)> SeedAsync(
        bool isActive = true, EDatabaseKind kind = EDatabaseKind.Shared)
    {
        var database = new CatalogDatabase
        {
            Id = Guid.NewGuid(), Name = $"db_{Guid.NewGuid():N}", Kind = kind, CreatedAt = UtcNow()
        };
        var tenant = new CatalogTenant
        {
            Id = Guid.NewGuid(), Cnpj = Guid.NewGuid().ToString("N")[..18], Name = "Empresa de Teste",
            IsActive = isActive, DatabaseId = database.Id, CreatedAt = UtcNow()
        };

        await using var write = _fixture.CreateCatalogContext(asMigrator: true);
        write.Databases.Add(database);
        write.Tenants.Add(tenant);
        await write.SaveChangesAsync();
        return (database, tenant);
    }

    private TenantCatalog CreateCatalog(out CatalogDbContext context)
    {
        context = _fixture.CreateCatalogContext(asMigrator: false);
        return new TenantCatalog(context);
    }

    [Fact]
    public async Task GetTenantByIdAsync_devolve_o_tenant_com_a_sua_base()
    {
        var (database, tenant) = await SeedAsync(kind: EDatabaseKind.Dedicated);
        var catalog = CreateCatalog(out var context);
        await using var _ = context;

        var result = await catalog.GetTenantByIdAsync(tenant.Id);

        Assert.Equal(
            new TenantInfo(tenant.Id, tenant.Cnpj, "Empresa de Teste", true,
                new TenantDatabaseInfo(database.Name, EDatabaseKind.Dedicated)),
            result);
    }

    [Fact]
    public async Task GetTenantByIdAsync_devolve_null_quando_nao_existe()
    {
        var catalog = CreateCatalog(out var context);
        await using var _ = context;

        Assert.Null(await catalog.GetTenantByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTenantByIdAsync_devolve_tenant_inativo_com_IsActive_falso()
    {
        var (_, tenant) = await SeedAsync(isActive: false);
        var catalog = CreateCatalog(out var context);
        await using var _ctx = context;

        var result = await catalog.GetTenantByIdAsync(tenant.Id);

        Assert.NotNull(result);
        Assert.False(result!.IsActive);
    }

    [Fact]
    public async Task GetActiveTenantListAsync_devolve_so_ativos_paginados_por_chave()
    {
        var ativos = new List<Guid>();
        for (var i = 0; i < 5; i++) ativos.Add((await SeedAsync()).Tenant.Id);
        var inativo = (await SeedAsync(isActive: false)).Tenant.Id;

        var catalog = CreateCatalog(out var context);
        await using var _ = context;

        // Percorre todas as páginas de 2 em 2 pela chave (id > último id da página anterior).
        var todos = new List<TenantInfo>();
        Guid? depoisDe = null;
        while (true)
        {
            var pagina = await catalog.GetActiveTenantListAsync(depoisDe, size: 2);
            Assert.True(pagina.Count <= 2);
            if (pagina.Count == 0) break;
            todos.AddRange(pagina);
            depoisDe = pagina[^1].Id;
        }

        var ids = todos.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());                 // sem repetição entre páginas
        Assert.All(ativos, id => Assert.Contains(id, ids));              // todos os ativos aparecem
        Assert.DoesNotContain(inativo, ids);                             // o inativo não
        Assert.All(todos, t => Assert.True(t.IsActive));

        // A ordem é a do banco (ORDER BY id): confere contra a consulta crua.
        var ordemDoBanco = await DatabaseProbe.ReadStringsAsync(
            _fixture.CatalogMigratorConnectionString, "select id::text from tenants where is_active order by id");
        Assert.Equal(ordemDoBanco, ids.Select(id => id.ToString()).ToList());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GetActiveTenantListAsync_rejeita_size_fora_de_1_a_100(int size)
    {
        var catalog = CreateCatalog(out var context);
        await using var _ = context;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => catalog.GetActiveTenantListAsync(null, size));
    }
}
