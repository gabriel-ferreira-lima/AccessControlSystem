using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Migrator;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AccessControlSystem.Tests.Migrator;

// Chamam MigratorApplication.RunAsync direto (sem subir processo e sem InternalsVisibleTo): é o mesmo
// caminho do Program.cs, com a configuração injetada em memória.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class MigratorProgramTests
{
    private readonly PostgresContainerFixture _fixture;

    public MigratorProgramTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static Action<IConfigurationBuilder> Config(Dictionary<string, string?> values) =>
        builder => builder.AddInMemoryCollection(values);

    /// <summary>Catálogo novo, sem migrations (o próprio migrate as aplica), com uma base de tenant real cadastrada.</summary>
    private async Task<(DatabaseConnectionStrings Catalog, DatabaseConnectionStrings Tenant, Guid DatabaseId, Dictionary<string, string?> Settings)>
        NewEnvironmentAsync(int lockTimeoutSeconds = 30)
    {
        var catalog = await _fixture.CreateIsolatedCatalogAsync(migrate: true);
        var tenant = await _fixture.CreateEmptyDatabaseAsync($"prog_{Guid.NewGuid():N}");
        var database = (await MigratorTestSupport.AddDatabasesAsync(_fixture, catalog.Migrator, 1, $"prog_{Guid.NewGuid():N}"))[0];

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:CatalogMigration"] = catalog.Migrator,
            [$"TenantDatabases:{database.Name}:MigrationConnectionString"] = tenant.Migrator,
            ["Migrator:LockTimeoutSeconds"] = lockTimeoutSeconds.ToString(),
            ["DOTNET_ENVIRONMENT"] = "Development"
        };
        return (catalog, tenant, database.Id, settings);
    }

    [Fact]
    public async Task Sai_com_0_quando_tudo_foi_atualizado()
    {
        var (catalog, _, databaseId, settings) = await NewEnvironmentAsync();
        var output = new StringWriter();

        var code = await MigratorApplication.RunAsync(["migrate"], Config(settings), output: output);

        Assert.Equal(ExitCodes.Ok, code);
        var stored = await MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog.Migrator, databaseId);
        Assert.Equal(EMigrationStatus.Succeeded, stored.MigrationStatus);
        Assert.Equal(AppMigrations.LatestId(), stored.SchemaVersion);
        // O resumo sai em português e nunca mostra connection string nem senha.
        var text = output.ToString();
        Assert.Contains("atualizada", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PostgresRoleBootstrap.MigratorPassword, text);
    }

    [Fact]
    public async Task Sai_com_1_quando_alguma_base_falhou()
    {
        var (_, tenant, databaseId, settings) = await NewEnvironmentAsync();
        var databaseName = settings.Keys.Single(k => k.EndsWith(":MigrationConnectionString")).Split(':')[1];
        settings[$"TenantDatabases:{databaseName}:MigrationConnectionString"] =
            tenant.Migrator.Replace(PostgresRoleBootstrap.MigratorPassword, "senha-errada");
        var output = new StringWriter();

        var code = await MigratorApplication.RunAsync(["migrate"], Config(settings), output: output);

        Assert.Equal(ExitCodes.SomeDatabaseFailed, code);
        Assert.Contains(databaseName, output.ToString());          // mostra qual base falhou
        Assert.DoesNotContain("senha-errada", output.ToString());  // sem segredo
    }

    [Theory]
    [InlineData("sem_catalogo")]
    [InlineData("tamanho_de_lote_zero")]
    [InlineData("concorrencia_zero")]
    [InlineData("trava_zero")]
    [InlineData("verbo_desconhecido")]
    [InlineData("sem_verbo")]
    public async Task Sai_com_2_com_configuracao_invalida(string problem)
    {
        var (_, _, _, settings) = await NewEnvironmentAsync();
        var args = new[] { "migrate" };
        switch (problem)
        {
            case "sem_catalogo": settings["ConnectionStrings:CatalogMigration"] = null; break;
            case "tamanho_de_lote_zero": settings["Migrator:BatchSize"] = "0"; break;
            case "concorrencia_zero": settings["Migrator:MaxDegreeOfParallelism"] = "0"; break;
            case "trava_zero": settings["Migrator:LockTimeoutSeconds"] = "0"; break;
            case "verbo_desconhecido": args = ["apagar-tudo"]; break;
            case "sem_verbo": args = []; break;
        }
        var output = new StringWriter();

        var code = await MigratorApplication.RunAsync(args, Config(settings), output: output);

        Assert.Equal(ExitCodes.InvalidConfiguration, code);
        Assert.False(string.IsNullOrWhiteSpace(output.ToString()));   // explica o que está errado
        Assert.DoesNotContain(PostgresRoleBootstrap.MigratorPassword, output.ToString());
    }

    [Fact]
    public async Task Sai_com_4_quando_a_trava_global_esgota_o_tempo()
    {
        var (catalog, _, databaseId, settings) = await NewEnvironmentAsync(lockTimeoutSeconds: 1);

        // Outra execução segura a trava global do catálogo.
        await using var holder = new NpgsqlConnection(catalog.Migrator);
        await holder.OpenAsync();
        await using (var command = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", holder))
        {
            command.Parameters.AddWithValue("key", MigrationOrchestrator.GlobalLockKey);
            await command.ExecuteNonQueryAsync();
        }

        var code = await MigratorApplication.RunAsync(["migrate"], Config(settings), output: new StringWriter());

        Assert.Equal(ExitCodes.LockTimeout, code);
        // Nada foi atualizado enquanto a trava estava com outro.
        var stored = await MigratorTestSupport.ReadDatabaseAsync(_fixture, catalog.Migrator, databaseId);
        Assert.Equal(EMigrationStatus.Pending, stored.MigrationStatus);
    }

    [Fact]
    public async Task Registra_o_tenant_inicial_antes_de_atualizar_as_bases()
    {
        var (catalog, _, _, settings) = await NewEnvironmentAsync();
        var initialDatabase = await _fixture.CreateEmptyDatabaseAsync($"inicial_{Guid.NewGuid():N}");
        var initialId = Guid.NewGuid();
        var databaseName = $"inicial_{Guid.NewGuid():N}";
        settings["InitialTenant:Id"] = initialId.ToString();
        settings["InitialTenant:Cnpj"] = Guid.NewGuid().ToString("N")[..18];
        settings["InitialTenant:Name"] = "Empresa Inicial";
        settings["InitialTenant:DatabaseName"] = databaseName;
        settings[$"TenantDatabases:{databaseName}:MigrationConnectionString"] = initialDatabase.Migrator;

        var code = await MigratorApplication.RunAsync(["migrate"], Config(settings), output: new StringWriter());

        Assert.Equal(ExitCodes.Ok, code);
        await using var read = _fixture.CreateCatalogContext(catalog.Migrator);
        var registered = read.Tenants.Where(x => x.Id == initialId).Select(x => x.Database.Name).Single();
        Assert.Equal(databaseName, registered);
        // E a base do tenant inicial também foi atualizada (entrou no catálogo antes do lote).
        var stored = read.Databases.Single(x => x.Name == databaseName);
        Assert.Equal(EMigrationStatus.Succeeded, stored.MigrationStatus);
    }
}
