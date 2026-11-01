using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AccessControlSystem.Tests.Migrator;

// A SeedAdminPolicy impede que uma base NOVA receba o administrador de e-mail e senha fixos
// (FR-030/FR-031), sem editar a migration SeedAdmin já aplicada em bases antigas.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class SeedAdminPolicyTests
{
    private const string V1 = "20260907145357_v1";

    private readonly PostgresContainerFixture _fixture;

    public SeedAdminPolicyTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Base_sem_migrations_aplicadas_recebe_a_seed_admin_pre_marcada()
    {
        var database = await _fixture.CreateEmptyDatabaseAsync($"seedpolicy_{Guid.NewGuid():N}");
        await using var context = _fixture.CreateMigrationContext(database.Migrator);

        await SeedAdminPolicy.ApplyAsync(context);

        var historico = await DatabaseProbe.ReadHistoryAsync(database.Migrator);
        Assert.Equal(new[] { SeedAdminPolicy.SeedAdminMigrationId }, historico);
        Assert.Equal(0L, await DatabaseProbe.CountAsync(database.Migrator, "select count(*) from pg_tables where tablename = 'operators'"));
    }

    [Fact]
    public async Task Base_com_migrations_aplicadas_nao_e_alterada()
    {
        var database = await _fixture.CreateEmptyDatabaseAsync($"seedpolicy_{Guid.NewGuid():N}");
        await using var context = _fixture.CreateMigrationContext(database.Migrator);
        await context.GetService<IMigrator>().MigrateAsync(V1);   // base "já com histórico": só a v1

        await SeedAdminPolicy.ApplyAsync(context);

        var historico = await DatabaseProbe.ReadHistoryAsync(database.Migrator);
        Assert.Equal(new[] { V1 }, historico);   // a SeedAdmin NÃO foi pré-marcada
    }

    [Fact]
    public async Task Politica_e_idempotente()
    {
        var database = await _fixture.CreateEmptyDatabaseAsync($"seedpolicy_{Guid.NewGuid():N}");
        await using var context = _fixture.CreateMigrationContext(database.Migrator);

        await SeedAdminPolicy.ApplyAsync(context);
        await SeedAdminPolicy.ApplyAsync(context);

        var historico = await DatabaseProbe.ReadHistoryAsync(database.Migrator);
        Assert.Equal(new[] { SeedAdminPolicy.SeedAdminMigrationId }, historico);
    }
}
