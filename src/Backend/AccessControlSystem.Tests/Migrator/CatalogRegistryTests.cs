using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Migrator;

// O CatalogRegistry é a única fonte da verdade sobre a versão e o estado de cada base (FR-023).
// Cada teste usa o seu próprio catálogo, para contar e listar sem interferência de outros testes.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class CatalogRegistryTests
{
    private static readonly string Latest = AppMigrations.LatestId();

    private readonly PostgresContainerFixture _fixture;

    public CatalogRegistryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private async Task<(CatalogRegistry Registry, string Catalog)> NewRegistryAsync()
    {
        var catalog = await _fixture.CreateIsolatedCatalogAsync();
        return (new CatalogRegistry(PostgresContainerFixture.CatalogOptions(catalog.Migrator)), catalog.Migrator);
    }

    private Task<CatalogDatabase> ReadAsync(string catalog, Guid id) =>
        MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog, id);

    [Fact]
    public async Task MarkRunning_grava_situacao_e_data_da_tentativa()
    {
        var (registry, catalog) = await NewRegistryAsync();
        var database = (await MigratorTestSupport.AddDatabasesAsync(_fixture, catalog, 1, "run"))[0];

        await registry.MarkRunningAsync(database.Id);

        var stored = await ReadAsync(catalog, database.Id);
        Assert.Equal(EMigrationStatus.Running, stored.MigrationStatus);
        Assert.NotNull(stored.MigrationAttemptedAt);
        Assert.Equal(DateTimeKind.Utc, stored.MigrationAttemptedAt!.Value.Kind);
        Assert.InRange(stored.MigrationAttemptedAt.Value, DateTime.UtcNow.AddMinutes(-2), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task MarkSucceeded_grava_a_versao()
    {
        var (registry, catalog) = await NewRegistryAsync();
        var database = (await MigratorTestSupport.AddDatabasesAsync(_fixture, catalog, 1, "ok"))[0];
        await registry.MarkRunningAsync(database.Id);
        await registry.MarkFailedAsync(database.Id, "falha anterior");

        await registry.MarkSucceededAsync(database.Id, Latest);

        var stored = await ReadAsync(catalog, database.Id);
        Assert.Equal(EMigrationStatus.Succeeded, stored.MigrationStatus);
        Assert.Equal(Latest, stored.SchemaVersion);
        Assert.Null(stored.MigrationError);   // o motivo da falha anterior não fica para trás
    }

    [Fact]
    public async Task MarkFailed_grava_o_motivo_truncado_em_1000_caracteres()
    {
        var (registry, catalog) = await NewRegistryAsync();
        var database = (await MigratorTestSupport.AddDatabasesAsync(_fixture, catalog, 1, "fail"))[0];

        await registry.MarkFailedAsync(database.Id, new string('x', 1500));

        var stored = await ReadAsync(catalog, database.Id);
        Assert.Equal(EMigrationStatus.Failed, stored.MigrationStatus);
        Assert.Equal(1000, stored.MigrationError!.Length);
    }

    [Fact]
    public async Task MarkFailed_nao_apaga_a_ultima_versao_aplicada()
    {
        var (registry, catalog) = await NewRegistryAsync();
        var database = (await MigratorTestSupport.AddDatabasesAsync(
            _fixture, catalog, 1, "keep", EMigrationStatus.Succeeded, "versao_antiga"))[0];

        await registry.MarkFailedAsync(database.Id, "falhou na nova migration");

        // A versão registrada continua sendo a última que de fato foi aplicada.
        Assert.Equal("versao_antiga", (await ReadAsync(catalog, database.Id)).SchemaVersion);
    }

    [Fact]
    public async Task Base_elegivel_quando_versao_defasada_ou_situacao_diferente_de_Succeeded()
    {
        var (registry, catalog) = await NewRegistryAsync();
        await using (var context = _fixture.CreateCatalogContext(catalog))
        {
            CatalogDatabase New(string name, EMigrationStatus status, string? version) => new()
            {
                Id = Guid.NewGuid(), Name = name, Kind = EDatabaseKind.Shared,
                MigrationStatus = status, SchemaVersion = version, CreatedAt = DateTime.UtcNow
            };

            context.Databases.AddRange(
                New("em_dia", EMigrationStatus.Succeeded, Latest),
                New("defasada", EMigrationStatus.Succeeded, "20260910225125_AddIsActiveToOperator"),
                New("sem_versao", EMigrationStatus.Succeeded, null),
                New("pendente", EMigrationStatus.Pending, null),
                New("falhou", EMigrationStatus.Failed, Latest),
                New("travada", EMigrationStatus.Running, Latest));
            await context.SaveChangesAsync();
        }

        var eligible = await registry.ListEligibleAsync(null, 100, Latest);

        Assert.Equal(
            new[] { "defasada", "falhou", "pendente", "sem_versao", "travada" },
            eligible.Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Listagem_em_lotes_por_chave()
    {
        var (registry, catalog) = await NewRegistryAsync();
        await MigratorTestSupport.AddDatabasesAsync(_fixture, catalog, 25, "lote");

        // Ordem de referência: a do próprio banco (a chave é o id).
        await using var context = _fixture.CreateCatalogContext(catalog);
        var expected = await context.Databases.OrderBy(x => x.Id).Select(x => x.Id).ToListAsync();

        var seen = new List<Guid>();
        var pageSizes = new List<int>();
        Guid? after = null;
        while (true)
        {
            var page = await registry.ListEligibleAsync(after, 10, Latest);
            if (page.Count == 0) break;
            pageSizes.Add(page.Count);
            seen.AddRange(page.Select(x => x.Id));
            after = page[^1].Id;
        }

        Assert.Equal(new[] { 10, 10, 5 }, pageSizes);
        Assert.Equal(expected, seen);   // sem repetir nem pular nenhuma, na ordem da chave
    }

    [Fact]
    public async Task CountAsync_conta_todas_as_bases_do_catalogo()
    {
        var (registry, catalog) = await NewRegistryAsync();
        await MigratorTestSupport.AddDatabasesAsync(_fixture, catalog, 3, "a", EMigrationStatus.Succeeded, Latest);
        await MigratorTestSupport.AddDatabasesAsync(_fixture, catalog, 2, "b");

        Assert.Equal(5, await registry.CountAsync());
    }
}
