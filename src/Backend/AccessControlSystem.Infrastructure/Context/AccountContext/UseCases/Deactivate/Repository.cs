using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Deactivate {
    public class Repository : Application.Context.AccountContext.UseCases.Deactivate.Contracts.IRepository {

        private readonly AppDbContext _context;

        public Repository(AppDbContext context) {
            _context = context;
        }

        public async Task<Operator?> GetUserById(Guid id, CancellationToken cancellationToken = default) {
            return await _context.Operators.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task DeactivateAsync(Operator @operator, CancellationToken cancellationToken = default) {
            _context.Operators.Update(@operator);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<bool> IsLastActiveAdminAsync(Guid exceptId, CancellationToken cancellationToken = default) {
            var otherActiveAdmins = await _context.Operators
                .AsNoTracking()
                .CountAsync(x => x.Role == ERole.Admin && x.IsActive && x.Id != exceptId, cancellationToken);

            return otherActiveAdmins == 0;
        }
    }
}
