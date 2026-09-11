using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate {
    public class Repository : IRepository {
        private readonly AppDbContext _context;

        public Repository(AppDbContext context) {
            _context = context;
        }
        public async Task<Operator?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default) {
            return await _context.Operators.AsNoTracking().FirstOrDefaultAsync(x => x.Email.Address == email, cancellationToken);
        }
    }
}
