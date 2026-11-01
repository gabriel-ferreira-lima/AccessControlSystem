using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace AccessControlSystem.Infrastructure.Data {

    /// <summary>
    /// Aplica o isolamento de tenant a TODA entidade do <see cref="AppDbContext"/> (menos as
    /// <i>owned</i>, que vivem na tabela do dono): propriedade sombra <c>tenant_id</c>, filtro global
    /// por tenant e índice que começa por <c>tenant_id</c>. É a camada do EF; o RLS no banco é a outra.
    /// </summary>
    public static class TenantModelBuilderExtensions {

        public const string TenantIdPropertyName = "TenantId";

        public static ModelBuilder ApplyTenantIsolation(this ModelBuilder modelBuilder, AppDbContext context) {

            var tenantEntityTypes = modelBuilder.Model.GetEntityTypes()
                .Where(entityType => !entityType.IsOwned())
                .ToList();

            foreach (var entityType in tenantEntityTypes) {
                var builder = modelBuilder.Entity(entityType.ClrType);

                // O TenantId NÃO existe na entidade de domínio: é só uma propriedade sombra.
                builder.Property<Guid>(TenantIdPropertyName)
                    .HasColumnName("tenant_id")
                    .HasColumnType("uuid")
                    .IsRequired();

                // Índice que começa por tenant_id: atende a busca por id e a lista paginada do tenant.
                var keyName = entityType.FindPrimaryKey()!.Properties.Single().Name;
                builder.HasIndex(TenantIdPropertyName, keyName)
                    .HasDatabaseName($"ix_{entityType.GetTableName()}_tenant_id_id");

                // Filtro global: e.TenantId == context.CurrentTenantId. A expressão referencia a
                // PROPRIEDADE DA INSTÂNCIA do contexto, então o EF a avalia a cada consulta, para o
                // contexto que a executa, e não congela o tenant no modelo (que é cacheado).
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var tenantOfEntity = Expression.Call(
                    typeof(EF), nameof(EF.Property), new[] { typeof(Guid) },
                    parameter, Expression.Constant(TenantIdPropertyName));
                var currentTenant = Expression.Property(Expression.Constant(context), nameof(AppDbContext.CurrentTenantId));
                var filter = Expression.Lambda(Expression.Equal(tenantOfEntity, currentTenant), parameter);

                builder.HasQueryFilter(filter);
            }

            return modelBuilder;
        }
    }
}
