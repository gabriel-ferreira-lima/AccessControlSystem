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
                b.HasIndex(x => x.Address)
                    .IsUnique();
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
