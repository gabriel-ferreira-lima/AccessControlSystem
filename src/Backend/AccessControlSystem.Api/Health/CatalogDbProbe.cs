using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Api.Health {

    /// <summary>
    /// Sonda real do catálogo: lê UMA linha de <c>tenant_databases</c> (o equivalente a
    /// <c>SELECT 1 FROM tenant_databases LIMIT 1</c>), com o papel da aplicação (<c>acs_app</c>). Prova três coisas: o
    /// catálogo alcança, a credencial vale e o esquema existe. As bases de tenant não entram (são muitas).
    /// </summary>
    public class CatalogDbProbe : ICatalogProbe {

        private readonly CatalogDbContext _context;

        public CatalogDbProbe(CatalogDbContext context) {
            _context = context;
        }

        /// <remarks>Qualquer exceção (rede, credencial, esquema ausente, tempo limite) sobe para o verificador.</remarks>
        public async Task ProbeAsync(CancellationToken cancellationToken) {
            await _context.Databases
                .AsNoTracking()
                .Select(database => database.Id)
                .Take(1)
                .ToListAsync(cancellationToken);
        }
    }
}
