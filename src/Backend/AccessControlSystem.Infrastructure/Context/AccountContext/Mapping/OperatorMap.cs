using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.Mapping {
    public class OperatorMap : IEntityTypeConfiguration<Operator> {
        public void Configure(EntityTypeBuilder<Operator> builder) {

            builder.ToTable("operators");

            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id)
                .HasColumnName("id")
                .ValueGeneratedNever();

            builder.OwnsOne(x => x.Email, b => {
                b.Property(x => x.Address)
                    .HasColumnName("email")
                    .IsRequired(true)
                    .HasColumnType("varchar(255)");

                // A unicidade do e-mail é POR TENANT: o índice único (tenant_id, email) vive só na
                // migration EnforceTenantIsolationOnOperators. O EF não aceita um índice que misture
                // a propriedade do dono (tenant_id) com a do owned type (e-mail) em table splitting.
                // A regra é protegida por teste de integração (SchemaContractTests).
            });

            builder.OwnsOne(x => x.Password)
                .Property(x => x.Hash)
                .HasColumnName("password")
                .HasColumnType("varchar(255)")
                .IsRequired(true);

            builder.Property(x => x.Role)
                .HasColumnName("role")
                .HasConversion<short>()
                .HasColumnType("SMALLINT")
                .IsRequired(true);

            builder.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true)
                .IsRequired(true);
        }
    }
}
