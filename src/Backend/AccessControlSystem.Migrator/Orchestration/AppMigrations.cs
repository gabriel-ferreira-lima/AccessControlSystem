using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>Quais migrations o código conhece para as bases de tenant (sem tocar em nenhum banco).</summary>
    public static class AppMigrations {

        /// <summary>Id da última migration do <see cref="AppDbContext"/>: a versão que toda base deve ter.</summary>
        public static string LatestId() {
            // Só lê os assemblies: a connection string nunca é usada.
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql("Host=nao-usada", npgsql => npgsql.MigrationsAssembly("AccessControlSystem.Api"))
                .Options;

            using var context = new AppDbContext(options, new NoTenantContext());
            return context.Database.GetMigrations().Last();
        }
    }
}
