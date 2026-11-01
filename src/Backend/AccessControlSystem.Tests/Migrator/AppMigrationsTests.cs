using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Migrator;

// A versão que o catálogo registra como "em dia" é a última migration que o código conhece.
public class AppMigrationsTests
{
    [Fact]
    public void LatestId_e_a_ultima_migration_do_AppDbContext()
    {
        var context = MigrationContextFactory.Create("Host=nao-usada");
        var known = context.Database.GetMigrations().OrderBy(x => x, StringComparer.Ordinal).ToList();

        Assert.Equal(known[^1], AppMigrations.LatestId());
    }
}
