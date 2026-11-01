using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Tests.Migrator;

// Orquestrador + executor REAL (EfDatabaseMigrationRunner) sobre bases reais do container.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class MigrationOrchestratorIntegrationTests
{
    private static readonly string Latest = AppMigrations.LatestId();

    private readonly PostgresContainerFixture _fixture;

    public MigrationOrchestratorIntegrationTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private MigrationOrchestrator CreateOrchestrator(DatabaseConnectionStrings catalog, TenantDatabasesOptions databases) =>
        new(new CatalogRegistry(PostgresContainerFixture.CatalogOptions(catalog.Migrator)),
            new EfDatabaseMigrationRunner(),
            Options.Create(databases),
            Options.Create(new MigratorOptions { BatchSize = 10, MaxDegreeOfParallelism = 2, LockTimeoutSeconds = 30 }),
            Options.Create(new CatalogMigrationOptions { CatalogMigration = catalog.Migrator }),
            Options.Create(new InitialTenantOptions()));

    private async Task<(CatalogDatabase Database, DatabaseConnectionStrings Strings)> AddRealDatabaseAsync(
        DatabaseConnectionStrings catalog, TenantDatabasesOptions databases, string prefix)
    {
        var strings = await _fixture.CreateEmptyDatabaseAsync($"{prefix}_{Guid.NewGuid():N}");
        var database = (await MigratorTestSupport.AddDatabasesAsync(_fixture, catalog.Migrator, 1, $"real_{Guid.NewGuid():N}"))[0];
        databases[database.Name] = new TenantDatabaseOptions { MigrationConnectionString = strings.Migrator };
        return (database, strings);
    }

    [Fact]
    public async Task Duas_bases_reais_sao_atualizadas_e_registradas()
    {
        var catalog = await _fixture.CreateIsolatedCatalogAsync();
        var databases = new TenantDatabasesOptions();
        var first = await AddRealDatabaseAsync(catalog, databases, "int1");
        var second = await AddRealDatabaseAsync(catalog, databases, "int2");

        var summary = await CreateOrchestrator(catalog, databases).RunAsync();

        Assert.Equal(2, summary.Updated);
        Assert.Equal(0, summary.Failed);
        foreach (var (database, strings) in new[] { first, second })
        {
            var stored = await MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog.Migrator, database.Id);
            Assert.Equal(EMigrationStatus.Succeeded, stored.MigrationStatus);
            Assert.Equal(Latest, stored.SchemaVersion);
            Assert.Equal(Latest, (await DatabaseProbe.ReadHistoryAsync(strings.Migrator))[^1]);
            Assert.Equal(0L, await DatabaseProbe.CountAsync(strings.Migrator, "select count(*) from operators"));   // FR-026
        }
    }

    [Fact]
    public async Task Base_com_connection_string_invalida_falha_e_a_outra_conclui()
    {
        var catalog = await _fixture.CreateIsolatedCatalogAsync();
        var databases = new TenantDatabasesOptions();
        var good = await AddRealDatabaseAsync(catalog, databases, "bom");
        var broken = await AddRealDatabaseAsync(catalog, databases, "ruim");
        const string secret = "senha-errada-do-teste-7c1d";
        databases[broken.Database.Name].MigrationConnectionString =
            broken.Strings.Migrator.Replace(PostgresRoleBootstrap.MigratorPassword, secret);

        var summary = await CreateOrchestrator(catalog, databases).RunAsync();

        Assert.Equal(1, summary.Updated);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(EMigrationStatus.Succeeded,
            (await MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog.Migrator, good.Database.Id)).MigrationStatus);
        var stored = await MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog.Migrator, broken.Database.Id);
        Assert.Equal(EMigrationStatus.Failed, stored.MigrationStatus);
        Assert.False(string.IsNullOrWhiteSpace(stored.MigrationError));
        Assert.DoesNotContain(secret, stored.MigrationError!);
    }

    [Fact]
    public async Task Reexecucao_retoma_so_a_base_que_falhou()
    {
        var catalog = await _fixture.CreateIsolatedCatalogAsync();
        var databases = new TenantDatabasesOptions();
        var good = await AddRealDatabaseAsync(catalog, databases, "bom");
        var broken = await AddRealDatabaseAsync(catalog, databases, "ruim");
        var correct = databases[broken.Database.Name].MigrationConnectionString!;
        databases[broken.Database.Name].MigrationConnectionString =
            correct.Replace(PostgresRoleBootstrap.MigratorPassword, "senha-errada");
        await CreateOrchestrator(catalog, databases).RunAsync();
        var goodAfterFirst = await MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog.Migrator, good.Database.Id);

        databases[broken.Database.Name].MigrationConnectionString = correct;   // a configuração foi corrigida
        var second = await CreateOrchestrator(catalog, databases).RunAsync();

        Assert.Equal(1, second.Updated);
        Assert.Equal(1, second.AlreadyCurrent);
        Assert.Equal(EMigrationStatus.Succeeded,
            (await MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog.Migrator, broken.Database.Id)).MigrationStatus);

        // A base que já estava em dia não foi tocada (mesma data de tentativa).
        var goodAfterSecond = await MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog.Migrator, good.Database.Id);
        Assert.Equal(goodAfterFirst.MigrationAttemptedAt, goodAfterSecond.MigrationAttemptedAt);
    }
}
