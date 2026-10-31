using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.GetMe {
    public class Repository : Application.Context.AccountContext.UseCases.GetMe.Contracts.IRepository {
        private readonly AppDbContext _context;

        public Repository(AppDbContext context) {
            _context = context;
        }

        public async Task<Operator?> GetUserById(Guid id, CancellationToken cancellationToken = default) {
            return await _context.Operators.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }
    }
}
