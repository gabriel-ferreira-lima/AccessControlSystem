using AccessControlSystem.Application.Context.AccountContext.UseCases.Get;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Enums;
using AccessControlSystem.Application.SharedContext.UseCases;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Get {
    public class Repository : Application.Context.AccountContext.UseCases.Get.Contracts.IRepository {

        private readonly AppDbContext _context;

        public Repository(AppDbContext context) {
            _context = context;
        }
        public async Task<PagedResponse<OperatorItem>> GetOperatorList(
            int page, int size, EActiveFilter isActive, CancellationToken cancellationToken) {

            var query = _context.Operators.AsQueryable();

            switch (isActive) {
                case EActiveFilter.Active:
                    query = query.Where(x => x.IsActive);
                    break;

                case EActiveFilter.Inactive:
                    query = query.Where(x => !x.IsActive);
                    break;

                default:
                    break;
            }

            query = query.OrderBy(x => x.Id);

            var total = await query.CountAsync(cancellationToken);

            var items = await query
                .AsNoTracking()
                .Skip((page - 1) * size)
                .Take(size)
                .Select(x => new OperatorItem(x.Id, x.Email.Address, x.Role, x.IsActive))
                .ToListAsync(cancellationToken);

            return new PagedResponse<OperatorItem>(items, page, size, total);
        }
    }
}
