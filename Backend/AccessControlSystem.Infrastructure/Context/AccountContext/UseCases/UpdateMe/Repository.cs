using AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe.Contracts;
using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.UpdateMe {

    public class Repository : IRepository{

        private readonly AppDbContext _context;

        public Repository(AppDbContext context) {
            _context = context;
        }

        public async Task<Operator> UpdateAsync(Operator @operator, CancellationToken cancellationToken = default) {
            try {
                _context.Operators.Update(@operator);
                await _context.SaveChangesAsync(cancellationToken);
                return @operator;
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) {
                throw new ConflictException("E-mail já está em uso");
            }
        }

        public async Task<Operator?> GetUserById(Guid id, CancellationToken cancellationToken = default) {
            return await _context.Operators.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<bool> CheckIfEmailIsNotUsed(string email, Guid id, CancellationToken cancellationToken = default) {
            return await _context.Operators.AsNoTracking().AnyAsync(x => x.Email.Address == email && x.Id != id, cancellationToken);
        }
    }
}
