using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AccessControlSystem.Api.DesignTime {

    /// <summary>
    /// Usada só pelas ferramentas do EF (<c>dotnet ef migrations add</c>) para gerar migrations do
    /// catálogo. A connection string pode ser fictícia: gerar a migration não precisa de banco.
    /// Aplicar migrations NÃO é feito por aqui (quem aplica é o Migrator).
    /// </summary>
    public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext> {

        public CatalogDbContext CreateDbContext(string[] args) {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__CatalogMigration")
                ?? "Host=localhost;Database=acs_catalog;Username=acs_migrator;Password=design-time";

            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("AccessControlSystem.Api"))
                .Options;

            return new CatalogDbContext(options);
        }
    }
}
