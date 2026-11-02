using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Migrator;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace AccessControlSystem.Tests.Migrator;

/// <summary>Ambiente isolado de teste do Migrator: catálogo próprio, duas bases de tenant e a configuração em memória.</summary>
public sealed record StagingEnvironment(
    DatabaseConnectionStrings Catalog,
    DatabaseConnectionStrings Shared,
    DatabaseConnectionStrings Dedicated,
    Dictionary<string, string?> Settings);

/// <summary>
/// Apoio dos testes dos verbos <c>seed-staging</c> e <c>verify-no-seed-admin</c> (infraestrutura de teste, sem teste
/// próprio): monta o ambiente isolado, roda um verbo pelo mesmo caminho do <c>Program.cs</c> e cria a base
/// "contaminada de verdade" (com o administrador fixo da migration <c>SeedAdmin</c>).
/// </summary>
public static class StagingTestSupport
{
    // Senhas de TESTE (o repositório público não guarda senha de operador fictício: estas valem só aqui, nos testes).
    public const string AdminPassword = "Admin!Stg123";
    public const string OperatorPasswordA = "Operador!Stg-A1";
    public const string OperatorPasswordB = "Operador!Stg-B2";

    /// <summary>
    /// Catálogo isolado (já migrado) e duas bases de tenant vazias (sem tabelas e sem registro no catálogo), como num
    /// ambiente novo. A configuração aponta <c>shared_01</c> e <c>dedicated_01</c> para elas.
    /// </summary>
    public static async Task<StagingEnvironment> NewEnvironmentAsync(
        PostgresContainerFixture fixture, string? environmentName = "Staging")
    {
        var catalog = await fixture.CreateIsolatedCatalogAsync();
        var shared = await fixture.CreateEmptyDatabaseAsync($"stg_shared_{Guid.NewGuid():N}");
        var dedicated = await fixture.CreateEmptyDatabaseAsync($"stg_dedicated_{Guid.NewGuid():N}");

        var settings = new Dictionary<string, string?>
        {
            ["DOTNET_ENVIRONMENT"] = environmentName,
            ["ConnectionStrings:CatalogMigration"] = catalog.Migrator,
            ["TenantDatabases:shared_01:ConnectionString"] = shared.App,
            ["TenantDatabases:shared_01:MigrationConnectionString"] = shared.Migrator,
            ["TenantDatabases:dedicated_01:ConnectionString"] = dedicated.App,
            ["TenantDatabases:dedicated_01:MigrationConnectionString"] = dedicated.Migrator,
            ["StagingSeed:AdminPassword"] = AdminPassword,
            ["StagingSeed:OperatorPasswordTenantA"] = OperatorPasswordA,
            ["StagingSeed:OperatorPasswordTenantB"] = OperatorPasswordB,
        };
        return new StagingEnvironment(catalog, shared, dedicated, settings);
    }

    /// <summary>Roda um verbo do Migrator com a configuração do ambiente (o mesmo caminho do <c>Program.cs</c>).</summary>
    public static Task<int> RunAsync(string verb, StagingEnvironment environment, TextWriter? output = null) =>
        RunAsync(verb, environment.Settings, output);

    public static Task<int> RunAsync(string verb, Dictionary<string, string?> settings, TextWriter? output = null) =>
        MigratorApplication.RunAsync(
            [verb],
            builder => builder.AddInMemoryCollection(settings),
            output: output ?? new StringWriter());

    /// <summary>
    /// Banco de tenant CONTAMINADO DE VERDADE: as migrations rodam direto (<c>Database.MigrateAsync()</c>), SEM a
    /// <c>SeedAdminPolicy</c>, e por isso a migration <c>SeedAdmin</c> é executada e cria o administrador fixo.
    /// Nunca usa <c>dotnet ef</c>. O tenant inicial (<paramref name="initialTenantId"/>) é informado na MESMA sessão,
    /// como o executor real faz, porque a migration seguinte atribui as linhas antigas a ele.
    /// </summary>
    public static async Task<DatabaseConnectionStrings> ContaminatedDatabaseAsync(
        PostgresContainerFixture fixture, string name, Guid? initialTenantId = null)
    {
        var database = await fixture.CreateEmptyDatabaseAsync(name);

        await using var context = fixture.CreateMigrationContext(database.Migrator);
        await context.Database.OpenConnectionAsync();
        await context.Database.ExecuteSqlAsync(
            $"SELECT set_config('acs.initial_tenant_id', {(initialTenantId ?? Guid.Parse("bbbbbbbb-0000-4000-8000-000000000001")).ToString()}, false)");
        await context.Database.MigrateAsync();

        return database;
    }

    /// <summary>Cadastra uma base no catálogo (pelo migrator) e devolve o id.</summary>
    public static async Task<Guid> RegisterDatabaseAsync(
        PostgresContainerFixture fixture, string catalogMigratorConnectionString, string name,
        EDatabaseKind kind = EDatabaseKind.Shared)
    {
        var id = Guid.NewGuid();
        await using var context = fixture.CreateCatalogContext(catalogMigratorConnectionString);
        context.Databases.Add(new CatalogDatabase { Id = id, Name = name, Kind = kind, CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        return id;
    }

    /// <summary>Aponta <c>TenantDatabases:{name}</c> da configuração para uma base.</summary>
    public static void ConfigureDatabase(Dictionary<string, string?> settings, string name, DatabaseConnectionStrings database)
    {
        settings[$"TenantDatabases:{name}:ConnectionString"] = database.App;
        settings[$"TenantDatabases:{name}:MigrationConnectionString"] = database.Migrator;
    }

    public sealed record OperatorRow(Guid? TenantId, string Email, string Password);

    /// <summary>Todos os operadores da base, pela conexão do migrator (BYPASSRLS). Vazio se a tabela não existe.</summary>
    public static async Task<List<OperatorRow>> ReadOperatorsAsync(string migratorConnectionString)
    {
        await using var connection = new NpgsqlConnection(migratorConnectionString);
        await connection.OpenAsync();

        await using (var exists = new NpgsqlCommand("select to_regclass('operators') is not null", connection))
        {
            if (!(bool)(await exists.ExecuteScalarAsync() ?? false)) return [];
        }

        await using var command = new NpgsqlCommand("select tenant_id, email, password from operators order by email, tenant_id", connection);
        await using var reader = await command.ExecuteReaderAsync();

        var rows = new List<OperatorRow>();
        while (await reader.ReadAsync())
        {
            rows.Add(new OperatorRow(
                reader.IsDBNull(0) ? null : reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2)));
        }
        return rows;
    }

    /// <summary>Quantas tabelas existem no esquema <c>public</c> (0 = base vazia, sem estrutura).</summary>
    public static Task<long> CountTablesAsync(string migratorConnectionString) =>
        DatabaseProbe.CountAsync(migratorConnectionString,
            "select count(*) from information_schema.tables where table_schema = 'public'");
}
