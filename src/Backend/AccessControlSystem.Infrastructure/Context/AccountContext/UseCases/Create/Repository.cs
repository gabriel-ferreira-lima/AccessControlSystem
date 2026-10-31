using AccessControlSystem.Application.Context.AccountContext.UseCases.Create.Contracts;
using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;


namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Create {
    public class Repository : IRepository {

        private readonly AppDbContext _context;
        public Repository(AppDbContext context) {
            _context = context;
        }

        public async Task<bool> AnyAsync(string email, CancellationToken cancellationToken = default) {
            return await _context.Operators.AsNoTracking().AnyAsync(x => x.Email.Address == email, cancellationToken);
        }

        public async Task SaveAsync(Operator @operator, CancellationToken cancellationToken = default) {
            try {
                await _context.Operators.AddAsync(@operator, cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23505" }) {
                throw new ConflictException("E-mail já está em uso");
            }
        }
    }
}