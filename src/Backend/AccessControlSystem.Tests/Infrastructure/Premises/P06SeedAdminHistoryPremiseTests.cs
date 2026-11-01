using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using System.Reflection;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

// P-06 (R-14): IHistoryRepository (API pública do EF) grava na base nova a linha da migration
// SeedAdmin como já aplicada, e o Migrate seguinte a pula. Assim uma base nova nunca recebe o
// administrador de e-mail e senha fixos (FR-030/FR-031), sem editar a migration.
[Collection(PremiseCollection.Name)]
[Trait("Category", "Integration")]
public class P06SeedAdminHistoryPremiseTests
{
    private const string SeedAdminMigrationId = "20260908171522_SeedAdmin";

    private readonly PremisePostgresFixture _fixture;

    public P06SeedAdminHistoryPremiseTests(PremisePostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Base_nova_nao_recebe_o_admin_de_seed()
    {
        var database = await _fixture.CreateEmptyDatabaseAsync($"p06_{Guid.NewGuid():N}");
        await using var context = _fixture.CreateMigrationContext(database.Migrator);

        // Pré-marca a SeedAdmin como aplicada, antes de qualquer migration.
        var history = context.GetService<IHistoryRepository>();
        await context.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript());
        await context.Database.ExecuteSqlRawAsync(
            history.GetInsertScript(new HistoryRow(SeedAdminMigrationId, EfProductVersion())));

        await context.Database.MigrateAsync();

        // Nenhum operador: o INSERT da SeedAdmin não rodou.
        Assert.Equal(0, await context.Operators.CountAsync());

        await using var connection = new NpgsqlConnection(database.Migrator);
        await connection.OpenAsync();

        // As demais migrations rodaram (a coluna da AddIsActiveToOperator existe).
        Assert.Equal(1L, await ScalarAsync(connection,
            "select count(*) from information_schema.columns where table_name = 'operators' and column_name = 'is_active'"));

        // O histórico é igual às migrations do assembly, e a SeedAdmin está nele por ter sido pré-marcada.
        var aplicadas = new List<string>();
        await using (var command = new NpgsqlCommand(
            "select \"MigrationId\" from \"__EFMigrationsHistory\" order by \"MigrationId\"", connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) aplicadas.Add(reader.GetString(0));
        }

        var conhecidas = context.Database.GetMigrations().OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(conhecidas, aplicadas);
        Assert.Contains(SeedAdminMigrationId, aplicadas);
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    // Mesma versão que o EF grava no histórico (informational version sem o sufixo "+commit").
    private static string EfProductVersion() =>
        typeof(DbContext).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion.Split('+')[0];
}
