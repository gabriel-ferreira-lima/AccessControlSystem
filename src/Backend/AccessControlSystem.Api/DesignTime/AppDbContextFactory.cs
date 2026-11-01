using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AccessControlSystem.Api.DesignTime {

    /// <summary>
    /// Usada só pelas ferramentas do EF (<c>dotnet ef migrations add</c>) para gerar migrations do
    /// <see cref="AppDbContext"/>. A connection string pode ser fictícia: gerar a migration não
    /// precisa de banco. Aplicar migrations NÃO é feito por aqui (quem aplica é o Migrator).
    /// </summary>
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext> {

        public AppDbContext CreateDbContext(string[] args) {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Migration")
                ?? "Host=localhost;Database=acs_shared_01;Username=acs_migrator;Password=design-time";

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("AccessControlSystem.Api"))
                .Options;

            return new AppDbContext(options, new NoTenantContext());
        }
    }
}
