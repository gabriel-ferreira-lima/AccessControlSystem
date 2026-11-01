using AccessControlSystem.Infrastructure.Catalog.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlSystem.Infrastructure.Catalog.Mapping {
    public class CatalogTenantMap : IEntityTypeConfiguration<CatalogTenant> {
        public void Configure(EntityTypeBuilder<CatalogTenant> builder) {

            builder.ToTable("tenants");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();   // o id é gerado pela aplicação, nunca pelo banco (FR-002)

            // O CNPJ é texto lido, sem validação aqui (o catálogo é do provisionamento). O tamanho
            // comporta o formato com máscara; a única garantia é a unicidade (FR-002).
            builder.Property(x => x.Cnpj)
                .HasColumnName("cnpj")
                .HasColumnType("varchar(18)")
                .IsRequired();
            builder.HasIndex(x => x.Cnpj)
                .IsUnique()
                .HasDatabaseName("ix_tenants_cnpj");

            builder.Property(x => x.Name)
                .HasColumnName("name")
                .HasColumnType("varchar(200)")
                .IsRequired();

            // ValueGeneratedNever: o EF sempre grava o valor informado (inclusive false) e o padrão
            // do banco só vale para quem inserir à mão.
            builder.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true)
                .ValueGeneratedNever()
                .IsRequired();

            builder.Property(x => x.DatabaseId)
                .HasColumnName("database_id")
                .IsRequired();
            builder.HasOne(x => x.Database)
                .WithMany()
                .HasForeignKey(x => x.DatabaseId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => x.DatabaseId)
                .HasDatabaseName("ix_tenants_database_id");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();

            // Paginação por chave dos tenants ativos (GetActiveTenantListAsync).
            builder.HasIndex(x => x.Id)
                .HasDatabaseName("ix_tenants_id_active")
                .HasFilter("is_active");

            // Concorrência otimista: a coluna de sistema xmin do PostgreSQL (editável, Operação).
            builder.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}
