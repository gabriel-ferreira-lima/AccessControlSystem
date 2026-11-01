using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Migrator;

/// <summary>Apoio dos testes do Migrator: bases cadastradas direto no catálogo, sem passar pelo Migrator.</summary>
public static class MigratorTestSupport
{
    /// <summary>Cadastra <paramref name="count"/> bases (nomes <c>prefixo_0001</c>...) no catálogo informado.</summary>
    public static async Task<List<CatalogDatabase>> AddDatabasesAsync(
        PostgresContainerFixture fixture,
        string catalogMigratorConnectionString,
        int count,
        string prefix,
        EMigrationStatus status = EMigrationStatus.Pending,
        string? schemaVersion = null)
    {
        var databases = Enumerable.Range(1, count)
            .Select(i => new CatalogDatabase
            {
                Id = Guid.NewGuid(),
                Name = $"{prefix}_{i:D4}",
                Kind = EDatabaseKind.Shared,
                MigrationStatus = status,
                SchemaVersion = schemaVersion,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        await using var context = fixture.CreateCatalogContext(catalogMigratorConnectionString);
        context.Databases.AddRange(databases);
        await context.SaveChangesAsync();
        return databases;
    }

    public static async Task<CatalogDatabase> ReadDatabaseAsync(
        PostgresContainerFixture fixture, string catalogMigratorConnectionString, Guid id)
    {
        await using var context = fixture.CreateCatalogContext(catalogMigratorConnectionString);
        return await context.Databases.AsNoTracking().SingleAsync(x => x.Id == id);
    }
}
