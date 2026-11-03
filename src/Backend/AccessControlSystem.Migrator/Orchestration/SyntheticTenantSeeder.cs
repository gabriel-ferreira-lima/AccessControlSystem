using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using AccessControlSystem.Migrator.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>Um tenant sintético: id e CNPJ fictícios (o catálogo não valida o CNPJ), e a base onde ele mora.</summary>
    public sealed record SyntheticTenant(Guid Id, string Cnpj, string Name, string DatabaseName, EDatabaseKind Kind);

    /// <summary>Um operador sintético de um tenant. A senha vem sempre de fora do repositório.</summary>
    public sealed record SyntheticOperator(string Email, ERole Role, string Password);

    /// <summary>Um tenant sintético e os operadores que ele recebe.</summary>
    public sealed record SyntheticTenantPlan(SyntheticTenant Tenant, IReadOnlyList<SyntheticOperator> Operators);

    /// <summary>
    /// Caminho de escrita dos dados sintéticos, compartilhado pelo <c>seed-dev</c> (Development) e pelo
    /// <c>seed-staging</c> (Staging): cada comando cuida da sua guarda de ambiente, das suas senhas e dos seus dados, e
    /// delega a este seeder o resto. Idempotente.
    /// </summary>
    /// <remarks>
    /// Os operadores são criados pelo <see cref="AppDbContext"/>, com o papel da aplicação (<c>acs_app</c>) e sob RLS,
    /// como a Api os criaria. Antes disso o seeder registra as bases e os tenants no catálogo e atualiza a estrutura das
    /// bases que acabou de cadastrar (com a <see cref="SeedAdminPolicy"/>, pelo orquestrador): o verbo <c>migrate</c> só
    /// alcança bases já registradas, e num ambiente novo o catálogo está vazio quando ele roda. Se a atualização de
    /// qualquer base falhar, <b>nenhum operador é criado</b> (o registro no catálogo pode ficar: é idempotente).
    /// </remarks>
    public class SyntheticTenantSeeder {

        private readonly DbContextOptions<CatalogDbContext> _catalogOptions;
        private readonly InitialTenantRegistrar _registrar;
        private readonly MigrationOrchestrator _orchestrator;
        private readonly TenantDatabasesOptions _databases;
        private readonly TextWriter _output;

        public SyntheticTenantSeeder(
            DbContextOptions<CatalogDbContext> catalogOptions,
            InitialTenantRegistrar registrar,
            MigrationOrchestrator orchestrator,
            IOptions<TenantDatabasesOptions> databases,
            TextWriter output) {
            _catalogOptions = catalogOptions;
            _registrar = registrar;
            _orchestrator = orchestrator;
            _databases = databases.Value;
            _output = output;
        }

        /// <summary>
        /// Registra, atualiza e popula. <paramref name="verb"/> e <paramref name="label"/> só entram nas mensagens
        /// ("seed-dev"/"desenvolvimento", "seed-staging"/"homologação"). Devolve 0, ou 1 se alguma base não foi atualizada.
        /// </summary>
        public async Task<int> SeedAsync(
            string verb, string label, IReadOnlyList<SyntheticTenantPlan> plans, CancellationToken cancellationToken = default) {

            // 1. Catálogo: estrutura e cadastro das bases e dos tenants (idempotente).
            await using (var catalog = new CatalogDbContext(_catalogOptions)) {
                await catalog.Database.MigrateAsync(cancellationToken);

                foreach (var plan in plans) {
                    await _registrar.RegisterAsync(catalog, new InitialTenantOptions {
                        Id = plan.Tenant.Id,
                        Cnpj = plan.Tenant.Cnpj,
                        Name = plan.Tenant.Name,
                        DatabaseName = plan.Tenant.DatabaseName,
                        DatabaseKind = plan.Tenant.Kind
                    }, cancellationToken);
                }
            }
            _output.WriteLine($"Bases e tenants de {label} registrados no catálogo.");

            // 2. Estrutura das bases recém-cadastradas (as já atualizadas são ignoradas).
            var summary = await _orchestrator.RunAsync(cancellationToken);
            if (summary.Failed > 0) {
                foreach (var failure in summary.Outcomes.Where(x => x.Reason is not null)) {
                    _output.WriteLine($"  - {failure.Name}: falhou ({failure.Reason})");
                }
                _output.WriteLine($"{verb} interrompido: nem todas as bases foram atualizadas.");
                return ExitCodes.SomeDatabaseFailed;
            }

            _output.WriteLine($"Bases atualizadas pelo {verb}: {summary.Updated}; já na versão atual: {summary.AlreadyCurrent}.");

            // 3. Operadores, pelo AppDbContext, como acs_app e sob RLS.
            foreach (var plan in plans) {
                await SeedOperatorsAsync(verb, plan, cancellationToken);
            }

            _output.WriteLine($"Dados de {label} prontos: {plans.Count} tenants (A e B na base compartilhada, D na dedicada).");
            return ExitCodes.Ok;
        }

        /// <summary>
        /// Valida uma senha pelas regras do sistema (value object <see cref="Password"/>). Senha inválida é erro de
        /// configuração: <see cref="ArgumentException"/> sem o VALOR na mensagem, antes de qualquer escrita.
        /// </summary>
        public static void ValidatePassword(string section, string key, string password) {
            try {
                _ = new Password(password ?? string.Empty);
            }
            catch (DomainException) {
                throw new ArgumentException($"{section}:{key} não atende aos requisitos de senha (mínimo 8 caracteres, com maiúscula, minúscula, número e símbolo).");
            }
        }

        private async Task SeedOperatorsAsync(string verb, SyntheticTenantPlan plan, CancellationToken cancellationToken) {
            var tenant = plan.Tenant;

            if (!_databases.TryGetValue(tenant.DatabaseName, out var entry) || string.IsNullOrWhiteSpace(entry.ConnectionString)) {
                throw new ArgumentException(
                    $"TenantDatabases:{tenant.DatabaseName}:ConnectionString é obrigatória para o {verb} (papel da aplicação).");
            }

            // O mesmo caminho da Api: TenantScope resolvido, AppDbContext com os interceptors de tenant.
            var scope = new TenantScope();
            scope.Set(tenant.Id, tenant.DatabaseName);

            var builder = new DbContextOptionsBuilder<AppDbContext>();
            TenantDbContextOptions.Configure(builder, entry.ConnectionString);

            foreach (var (email, role, password) in plan.Operators) {
                await using var context = new AppDbContext(builder.Options, scope);

                // Consulta antes de criar: reexecutar não duplica nem troca a senha de ninguém.
                if (await context.Operators.AnyAsync(x => x.Email.Address == email, cancellationToken)) {
                    continue;
                }

                context.Operators.Add(new Operator(new Email(email), new Password(password), role));
                await context.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
