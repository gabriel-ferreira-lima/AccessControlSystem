using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Activate {
    public class Repository : Application.Context.AccountContext.UseCases.Activate.Contracts.IRepository {

        private readonly AppDbContext _context;

        public Repository(AppDbContext context) {
            _context = context;
        }

        public async Task<Operator?> GetUserById(Guid id, CancellationToken cancellationToken = default) {
            return await _context.Operators.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task ActivateAsync(Operator @operator, CancellationToken cancellationToken = default) {
            _context.Operators.Update(@operator);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
