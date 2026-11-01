using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Infrastructure.Context.AccountContext.Mapping;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Data {

    /// <summary>
    /// Contexto dos dados de tenant. É criado por requisição, apontando para a base onde o tenant mora
    /// (compartilhada ou dedicada), e conhece o tenant corrente: preenche o <c>tenant_id</c> nas
    /// escritas, filtra por ele nas leituras e falha fechado quando o tenant não foi resolvido.
    /// </summary>
    public class AppDbContext : DbContext, ITenantAwareDbContext {

        private readonly ITenantContext _tenantContext;

        public AppDbContext(DbContextOptions<AppDbContext> options, ITenantContext tenantContext) : base(options) {
            _tenantContext = tenantContext;

            // Todo SaveChanges abre uma transação do EF (mesmo com um só comando): é nela que o
            // TenantTransactionInterceptor informa o tenant ao banco (set_config local, premissa P-01).
            Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
        }

        public ITenantContext TenantContext => _tenantContext;

        /// <summary>
        /// Tenant usado pelo filtro global. Sem tenant resolvido devolve <c>Guid.Empty</c>, que não
        /// casa com nenhuma linha (os interceptors já falham antes de a consulta ir ao banco).
        /// </summary>
        public Guid CurrentTenantId => _tenantContext.IsResolved ? _tenantContext.TenantId : Guid.Empty;

        public DbSet<Operator> Operators { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder) {
            modelBuilder.ApplyConfiguration(new OperatorMap());
            modelBuilder.ApplyTenantIsolation(this);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess) {
            ApplyTenantToChanges();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default) {
            ApplyTenantToChanges();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        // O tenant_id é preenchido AQUI, pela Infrastructure, a partir do tenant resolvido: quem chama
        // nunca informa (Princípio I). Entidades novas recebem o tenant corrente; entidades alteradas
        // nunca têm o tenant_id atualizado.
        private void ApplyTenantToChanges() {
            ChangeTracker.DetectChanges();

            var entries = ChangeTracker.Entries()
                .Where(entry => entry.State is EntityState.Added or EntityState.Modified
                                && entry.Metadata.FindProperty(TenantModelBuilderExtensions.TenantIdPropertyName) is not null)
                .ToList();

            if (entries.Count == 0) {
                return;
            }

            if (!_tenantContext.IsResolved) {
                throw new InvalidOperationException(
                    "O tenant da requisição ainda não foi resolvido: a gravação foi bloqueada antes de chegar ao banco.");
            }

            foreach (var entry in entries) {
                var tenantProperty = entry.Property(TenantModelBuilderExtensions.TenantIdPropertyName);

                if (entry.State == EntityState.Added) {
                    tenantProperty.CurrentValue = _tenantContext.TenantId;
                }
                else {
                    tenantProperty.IsModified = false;
                }
            }
        }
    }
}
