using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Services {
    public class ActiveSessionChecker : IActiveSessionChecker {

        private readonly AppDbContext _context;

        public ActiveSessionChecker(AppDbContext context) {
            _context = context;
        }

        public async Task<bool> IsActiveAsync(Guid operatorId, CancellationToken cancellationToken = default) {
            return await _context.Operators
                .AsNoTracking()
                .AnyAsync(x => x.Id == operatorId && x.IsActive, cancellationToken);
        }
    }
}
