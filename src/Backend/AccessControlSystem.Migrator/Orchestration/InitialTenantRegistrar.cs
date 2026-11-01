using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Migrator.Options;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>
    /// Registra no catálogo, com o usuário migrator, a base e o tenant inicial da adoção de uma base
    /// legada (FR-027/FR-028). Idempotente: rodar de novo com a mesma configuração não muda nada.
    /// </summary>
    public class InitialTenantRegistrar {

        public async Task RegisterAsync(
            CatalogDbContext catalog,
            InitialTenantOptions options,
            CancellationToken cancellationToken = default) {

            Validate(options);

            var existing = await catalog.Tenants
                .Include(x => x.Database)
                .SingleOrDefaultAsync(x => x.Id == options.Id, cancellationToken);

            if (existing is not null) {
                // O catálogo pertence ao provisionamento: um tenant já cadastrado nunca é sobrescrito
                // (nem reativado). Se a configuração diverge dele, é erro de operação, não ajuste silencioso.
                if (existing.Cnpj != options.Cnpj || existing.Database.Name != options.DatabaseName) {
                    throw new InvalidOperationException(
                        $"O tenant inicial {options.Id} já está cadastrado com CNPJ ou base diferentes dos configurados em {InitialTenantOptions.SectionName}.");
                }

                return;
            }

            var now = DateTime.UtcNow;

            var database = await catalog.Databases
                .SingleOrDefaultAsync(x => x.Name == options.DatabaseName, cancellationToken);

            if (database is null) {
                database = new CatalogDatabase {
                    Id = Guid.NewGuid(),
                    Name = options.DatabaseName,
                    Kind = options.DatabaseKind,
                    CreatedAt = now
                };
                catalog.Databases.Add(database);
            }

            // CNPJ como informado, sem validação: só a unicidade é imposta, pelo índice do banco. Base e
            // tenant entram na mesma transação, então uma recusa não deixa a base cadastrada pela metade.
            catalog.Tenants.Add(new CatalogTenant {
                Id = options.Id,
                Cnpj = options.Cnpj,
                Name = options.Name,
                IsActive = true,
                DatabaseId = database.Id,
                CreatedAt = now
            });

            await catalog.SaveChangesAsync(cancellationToken);
        }

        private static void Validate(InitialTenantOptions options) {
            if (options.Id == Guid.Empty) {
                throw new ArgumentException($"{InitialTenantOptions.SectionName}:Id é obrigatório.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.Cnpj)) {
                throw new ArgumentException($"{InitialTenantOptions.SectionName}:Cnpj é obrigatório.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.Name)) {
                throw new ArgumentException($"{InitialTenantOptions.SectionName}:Name é obrigatório.", nameof(options));
            }

            if (string.IsNullOrWhiteSpace(options.DatabaseName)) {
                throw new ArgumentException($"{InitialTenantOptions.SectionName}:DatabaseName é obrigatório.", nameof(options));
            }
        }
    }
}
