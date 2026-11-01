using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.Fixtures;

/// <summary>
/// Contexto de migrations (sem tenant e sem interceptors), como o Migrator usa. Fica num lugar só
/// para as duas fixtures.
/// </summary>
public static class MigrationContextFactory
{
    public static AppDbContext Create(string migratorConnectionString)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(migratorConnectionString, npg => npg.MigrationsAssembly("AccessControlSystem.Api"))
            .Options;

        return new AppDbContext(options, new NoTenantContext());
    }
}
