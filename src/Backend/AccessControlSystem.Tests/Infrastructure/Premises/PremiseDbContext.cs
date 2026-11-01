using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

/// <summary>
/// Contexto mínimo das premissas do R-14: uma tabela com <c>tenant_id</c> e RLS, os mesmos
/// interceptors de tenant que o <c>AppDbContext</c> vai usar e a transação automática em toda
/// escrita (<see cref="AutoTransactionBehavior.Always"/>).
/// </summary>
public sealed class PremiseDbContext : DbContext, ITenantAwareDbContext
{
    public PremiseDbContext(DbContextOptions<PremiseDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        TenantContext = tenantContext;
        Database.AutoTransactionBehavior = AutoTransactionBehavior.Always;
    }

    public ITenantContext TenantContext { get; }

    public DbSet<PremiseItem> Items => Set<PremiseItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PremiseItem>(builder =>
        {
            builder.ToTable("premise_items");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property<Guid>("TenantId").HasColumnName("tenant_id").IsRequired();
        });
    }
}
