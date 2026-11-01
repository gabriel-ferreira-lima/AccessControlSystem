using AccessControlSystem.Infrastructure.Catalog.Mapping;
using AccessControlSystem.Infrastructure.Catalog.Models;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Data {

    /// <summary>
    /// Contexto do catálogo (base própria, migrations próprias). O catálogo pertence ao sistema de
    /// provisionamento: a Api só o lê (papel acs_app com SELECT) e o Migrator escreve o estado de
    /// atualização de cada base. Não é dado de tenant: sem filtro nem RLS por tenant.
    /// </summary>
    public class CatalogDbContext : DbContext {

        public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) {
        }

        public DbSet<CatalogTenant> Tenants { get; set; } = null!;
        public DbSet<CatalogDatabase> Databases { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.ApplyConfiguration(new CatalogDatabaseMap());
            modelBuilder.ApplyConfiguration(new CatalogTenantMap());
        }
    }
}
