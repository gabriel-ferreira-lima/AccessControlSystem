using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Migrator.Commands {

    /// <summary>
    /// Verbo <c>seed-dev</c> (só em Development): bases, tenants e operadores sintéticos para os
    /// cenários de desenvolvimento (FR-034 a FR-036; data-model.md, seção 4). Idempotente.
    /// </summary>
    /// <remarks>
    /// Os operadores são criados pelo <see cref="AppDbContext"/>, com o papel da aplicação (<c>acs_app</c>)
    /// e sob RLS, como a Api os criaria. Antes disso o comando atualiza as bases que acabou de cadastrar:
    /// num ambiente novo o catálogo está vazio quando o <c>migrate</c> roda, então estas bases ainda não
    /// têm tabelas.
    /// </remarks>
    public class SeedDevCommand {

        public const string AdminEmail = "admin@admin.com.br";
        public const string OperatorEmail = "operador@exemplo.com";

        private const string SharedDatabase = "shared_01";
        private const string DedicatedDatabase = "dedicated_01";

        private sealed record DevTenant(Guid Id, string Cnpj, string Name, string DatabaseName, EDatabaseKind Kind);

        // Dados da seção 4 do data-model.md: ids e CNPJs sintéticos (os CNPJs não são validados).
        // Campos de instância, e não estáticos: a regra de não ter estado estático com dado de tenant vale para todo o código.
        private readonly DevTenant _tenantA = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), "11222333000181", "Tenant A (desenvolvimento)", SharedDatabase, EDatabaseKind.Shared);
        private readonly DevTenant _tenantB = new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"), "11444777000161", "Tenant B (desenvolvimento)", SharedDatabase, EDatabaseKind.Shared);
        private readonly DevTenant _tenantD = new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"), "11222333000262", "Tenant D (desenvolvimento)", DedicatedDatabase, EDatabaseKind.Dedicated);

        private readonly IHostEnvironment _environment;
        private readonly DbContextOptions<CatalogDbContext> _catalogOptions;
        private readonly InitialTenantRegistrar _registrar;
        private readonly MigrationOrchestrator _orchestrator;
        private readonly TenantDatabasesOptions _databases;
        private readonly DevSeedOptions _seed;
        private readonly TextWriter _output;

        public SeedDevCommand(
            IHostEnvironment environment,
            DbContextOptions<CatalogDbContext> catalogOptions,
            InitialTenantRegistrar registrar,
            MigrationOrchestrator orchestrator,
            IOptions<TenantDatabasesOptions> databases,
            IOptions<DevSeedOptions> seed,
            TextWriter output) {
            _environment = environment;
            _catalogOptions = catalogOptions;
            _registrar = registrar;
            _orchestrator = orchestrator;
            _databases = databases.Value;
            _seed = seed.Value;
            _output = output;
        }

        /// <summary>Devolve o código de saída: 0, 1 (alguma base falhou) ou 3 (recusado fora de Development).</summary>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default) {

            // A recusa vem antes de qualquer outra coisa: nada é lido, validado nem escrito.
            if (!_environment.IsDevelopment()) {
                _output.WriteLine(
                    $"seed-dev recusado: o ambiente é '{_environment.EnvironmentName}' e ele só roda em Development. Nada foi escrito.");
                return ExitCodes.SeedDevRefused;
            }

            // Senhas inválidas são erro de configuração, e param antes de escrever qualquer coisa.
            ValidatePassword(nameof(DevSeedOptions.AdminPassword), _seed.AdminPassword);
            ValidatePassword(nameof(DevSeedOptions.OperatorPasswordTenantA), _seed.OperatorPasswordTenantA);
            ValidatePassword(nameof(DevSeedOptions.OperatorPasswordTenantB), _seed.OperatorPasswordTenantB);

            // 1. Catálogo: estrutura e cadastro das bases e dos tenants (idempotente).
            await using (var catalog = new CatalogDbContext(_catalogOptions)) {
                await catalog.Database.MigrateAsync(cancellationToken);

                foreach (var tenant in new[] { _tenantA, _tenantB, _tenantD }) {
                    await _registrar.RegisterAsync(catalog, new InitialTenantOptions {
                        Id = tenant.Id,
                        Cnpj = tenant.Cnpj,
                        Name = tenant.Name,
                        DatabaseName = tenant.DatabaseName,
                        DatabaseKind = tenant.Kind
                    }, cancellationToken);
                }
            }
            _output.WriteLine("Bases e tenants de desenvolvimento registrados no catálogo.");

            // 2. Estrutura das bases recém-cadastradas (as já atualizadas são ignoradas).
            var summary = await _orchestrator.RunAsync(cancellationToken);
            if (summary.Failed > 0) {
                foreach (var failure in summary.Outcomes.Where(x => x.Reason is not null)) {
                    _output.WriteLine($"  - {failure.Name}: falhou ({failure.Reason})");
                }
                _output.WriteLine("seed-dev interrompido: nem todas as bases foram atualizadas.");
                return ExitCodes.SomeDatabaseFailed;
            }

            _output.WriteLine($"Bases atualizadas pelo seed-dev: {summary.Updated}; já na versão atual: {summary.AlreadyCurrent}.");

            // 3. Operadores, pelo AppDbContext, como acs_app e sob RLS.
            await SeedOperatorsAsync(_tenantA, cancellationToken,
                (AdminEmail, ERole.Admin, _seed.AdminPassword),
                (OperatorEmail, ERole.Operator, _seed.OperatorPasswordTenantA));
            await SeedOperatorsAsync(_tenantB, cancellationToken,
                (AdminEmail, ERole.Admin, _seed.AdminPassword),
                (OperatorEmail, ERole.Operator, _seed.OperatorPasswordTenantB));
            await SeedOperatorsAsync(_tenantD, cancellationToken,
                (AdminEmail, ERole.Admin, _seed.AdminPassword));

            _output.WriteLine("Dados de desenvolvimento prontos: 3 tenants (A e B na base compartilhada, D na dedicada).");
            return ExitCodes.Ok;
        }

        private async Task SeedOperatorsAsync(
            DevTenant tenant,
            CancellationToken cancellationToken,
            params (string Email, ERole Role, string Password)[] operators) {

            if (!_databases.TryGetValue(tenant.DatabaseName, out var entry) || string.IsNullOrWhiteSpace(entry.ConnectionString)) {
                throw new ArgumentException(
                    $"TenantDatabases:{tenant.DatabaseName}:ConnectionString é obrigatória para o seed-dev (papel da aplicação).");
            }

            // O mesmo caminho da Api: TenantScope resolvido, AppDbContext com os interceptors de tenant.
            var scope = new TenantScope();
            scope.Set(tenant.Id, tenant.DatabaseName);

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            TenantDbContextOptions.Configure(builder, entry.ConnectionString);

            foreach (var (email, role, password) in operators) {
                await using var context = new AppDbContext(builder.Options, scope);

                // Consulta antes de criar: reexecutar não duplica nem troca a senha de ninguém.
                if (await context.Operators.AnyAsync(x => x.Email.Address == email, cancellationToken)) {
                    continue;
                }

                context.Operators.Add(new Operator(new Email(email), new Password(password), role));
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        private static void ValidatePassword(string key, string password) {
            try {
                _ = new Password(password ?? string.Empty);
            }
            catch (DomainException) {
                throw new ArgumentException($"{DevSeedOptions.SectionName}:{key} não atende aos requisitos de senha (mínimo 8 caracteres, com maiúscula, minúscula, número e símbolo).");
            }
        }
    }
}
