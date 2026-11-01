using AccessControlSystem.Infrastructure.Catalog.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlSystem.Infrastructure.Catalog.Mapping {
    public class CatalogDatabaseMap : IEntityTypeConfiguration<CatalogDatabase> {
        public void Configure(EntityTypeBuilder<CatalogDatabase> builder) {

            builder.ToTable("tenant_databases");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();   // o id é gerado pela aplicação, nunca pelo banco

            // Referência da base (chave em TenantDatabases:{name}). Não é connection string.
            builder.Property(x => x.Name)
                .HasColumnName("name")
                .HasColumnType("varchar(100)")
                .IsRequired();
            builder.HasIndex(x => x.Name)
                .IsUnique()
                .HasDatabaseName("ix_tenant_databases_name");

            builder.Property(x => x.Kind)
                .HasColumnName("kind")
                .HasConversion<short>()
                .HasColumnType("smallint")
                .IsRequired();

            builder.Property(x => x.SchemaVersion)
                .HasColumnName("schema_version")
                .HasColumnType("varchar(150)");

            // ValueGeneratedNever: o EF sempre grava o valor informado; o padrão do banco (1 = Pending)
            // só vale para quem inserir à mão.
            builder.Property(x => x.MigrationStatus)
                .HasColumnName("migration_status")
                .HasConversion<short>()
                .HasColumnType("smallint")
                .HasDefaultValue(EMigrationStatus.Pending)
                .ValueGeneratedNever()
                .IsRequired();

            builder.Property(x => x.MigrationAttemptedAt)
                .HasColumnName("migration_attempted_at")
                .HasColumnType("timestamptz");

            // Motivo curto da última falha, sem connection string nem segredo.
            builder.Property(x => x.MigrationError)
                .HasColumnName("migration_error")
                .HasColumnType("varchar(1000)");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("timestamptz")
                .IsRequired();

            // Lotes do orquestrador: bases pendentes/falhas/defasadas, por chave.
            builder.HasIndex(x => new { x.MigrationStatus, x.Id })
                .HasDatabaseName("ix_tenant_databases_migration_status_id");

            // Concorrência otimista: a coluna de sistema xmin do PostgreSQL.
            builder.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}
