using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Services {

    /// <summary>
    /// Leitura do catálogo. Devolve só <c>record</c>s da Application (nunca o modelo de persistência)
    /// e usa o papel da aplicação, que só tem SELECT no catálogo.
    /// </summary>
    public class TenantCatalog : ITenantCatalog {

        private const int MaxPageSize = 100;

        private readonly CatalogDbContext _context;

        public TenantCatalog(CatalogDbContext context) {
            _context = context;
        }

        public async Task<TenantInfo?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default) {
            // Uma consulta só, com as duas chaves primárias (tenant e base).
            return await _context.Tenants
                .AsNoTracking()
                .Where(x => x.Id == tenantId)
                .Select(x => new TenantInfo(
                    x.Id,
                    x.Cnpj,
                    x.Name,
                    x.IsActive,
                    new TenantDatabaseInfo(x.Database.Name, x.Database.Kind)))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<TenantInfo>> GetActiveTenantListAsync(
            Guid? afterTenantId, int size, CancellationToken cancellationToken = default) {

            if (size < 1 || size > MaxPageSize) {
                throw new ArgumentOutOfRangeException(nameof(size), size, "O tamanho da página deve ser de 1 a 100.");
            }

            var query = _context.Tenants
                .AsNoTracking()
                .Where(x => x.IsActive);

            if (afterTenantId.HasValue) {
                var after = afterTenantId.Value;
                query = query.Where(x => x.Id.CompareTo(after) > 0);
            }

            // Paginação por chave (id > último id da página anterior), servida pelo índice parcial
            // ix_tenants_id_active.
            return await query
                .OrderBy(x => x.Id)
                .Take(size)
                .Select(x => new TenantInfo(
                    x.Id,
                    x.Cnpj,
                    x.Name,
                    x.IsActive,
                    new TenantDatabaseInfo(x.Database.Name, x.Database.Kind)))
                .ToListAsync(cancellationToken);
        }
    }
}
