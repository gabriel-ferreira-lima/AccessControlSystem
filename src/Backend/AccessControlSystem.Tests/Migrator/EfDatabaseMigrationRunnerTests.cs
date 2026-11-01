using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Migrator;

// O executor atualiza UMA base: aplica a SeedAdminPolicy, roda as migrations e devolve o resultado.
// O caso "base legada com a seed admin" fica na US3 (Admin_de_seed_legado_e_preservado_no_tenant_inicial):
// depois da US1, uma base legada só migra com o tenant inicial informado.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class EfDatabaseMigrationRunnerTests
{
    private readonly PostgresContainerFixture _fixture;

    public EfDatabaseMigrationRunnerTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private Task<DatabaseConnectionStrings> NewDatabaseAsync() =>
        _fixture.CreateEmptyDatabaseAsync($"runner_{Guid.NewGuid():N}");

    [Fact]
    public async Task Base_nova_termina_sem_operadores()
    {
        var database = await NewDatabaseAsync();

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        // FR-031: a base nova NÃO recebe o administrador de seed.
        Assert.Equal(0L, await DatabaseProbe.CountAsync(database.Migrator, "select count(*) from operators"));
    }

    [Fact]
    public async Task Base_nova_tem_todas_as_migrations_no_historico()
    {
        var database = await NewDatabaseAsync();

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        await using var context = _fixture.CreateMigrationContext(database.Migrator);
        var conhecidas = context.Database.GetMigrations().OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.Equal(conhecidas, await DatabaseProbe.ReadHistoryAsync(database.Migrator));
        Assert.Equal(conhecidas[^1], result.SchemaVersion);
    }

    [Fact]
    public async Task Runner_e_idempotente()
    {
        var database = await NewDatabaseAsync();
        var runner = new EfDatabaseMigrationRunner();

        var primeira = await runner.RunAsync(database.Migrator, CancellationToken.None);
        var segunda = await runner.RunAsync(database.Migrator, CancellationToken.None);

        Assert.True(primeira.Succeeded, primeira.Error);
        Assert.True(segunda.Succeeded, segunda.Error);
        Assert.Equal(primeira.SchemaVersion, segunda.SchemaVersion);
    }

    [Fact]
    public async Task Falha_de_conexao_vira_resultado_de_falha_sem_a_connection_string()
    {
        const string segredo = "segredo-unico-do-teste-9f3a";
        var connectionString =
            $"Host=base-que-nao-existe.invalid;Port=5432;Database=x;Username=acs_migrator;Password={segredo};Timeout=3";

        var result = await new EfDatabaseMigrationRunner().RunAsync(connectionString, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
        Assert.DoesNotContain(segredo, result.Error!);
        Assert.DoesNotContain(connectionString, result.Error!);
    }
}
