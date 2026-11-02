using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Services {
    public class SessionValidator : ISessionValidator {

        private readonly AppDbContext _context;

        public SessionValidator(AppDbContext context) {
            _context = context;
        }

        // Uma única ida ao banco (Princípio XIII): situação e versão de segurança na mesma consulta, sob o RLS do tenant.
        public async Task<bool> IsValidAsync(Guid operatorId, int securityVersion, CancellationToken cancellationToken = default) {
            return await _context.Operators
                .AsNoTracking()
                .AnyAsync(x => x.Id == operatorId && x.IsActive && x.SecurityVersion == securityVersion, cancellationToken);
        }
    }
}
