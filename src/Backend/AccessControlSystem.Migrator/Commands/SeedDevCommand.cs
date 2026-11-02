using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Migrator.Commands {

    /// <summary>
    /// Verbo <c>seed-dev</c> (só em Development): bases, tenants e operadores sintéticos para os
    /// cenários de desenvolvimento (FR-034 a FR-036; data-model.md, seção 4). Idempotente.
    /// </summary>
    /// <remarks>
    /// Este comando cuida só da guarda de ambiente, das senhas e dos dados de desenvolvimento. O caminho de escrita
    /// (registrar no catálogo, atualizar as bases recém-cadastradas e criar os operadores pelo <c>AppDbContext</c>, sob
    /// RLS, como <c>acs_app</c>) é do <see cref="SyntheticTenantSeeder"/>, que o <c>seed-staging</c> também usa.
    /// </remarks>
    public class SeedDevCommand {

        public const string AdminEmail = "admin@admin.com.br";
        public const string OperatorEmail = "operador@exemplo.com";

        private const string SharedDatabase = "shared_01";
        private const string DedicatedDatabase = "dedicated_01";

        // Dados da seção 4 do data-model.md: ids e CNPJs sintéticos (os CNPJs não são validados).
        // Campos de instância, e não estáticos: a regra de não ter estado estático com dado de tenant vale para todo o código.
        private readonly SyntheticTenant _tenantA = new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"), "11222333000181", "Tenant A (desenvolvimento)", SharedDatabase, EDatabaseKind.Shared);
        private readonly SyntheticTenant _tenantB = new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"), "11444777000161", "Tenant B (desenvolvimento)", SharedDatabase, EDatabaseKind.Shared);
        private readonly SyntheticTenant _tenantD = new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"), "11222333000262", "Tenant D (desenvolvimento)", DedicatedDatabase, EDatabaseKind.Dedicated);

        private readonly IHostEnvironment _environment;
        private readonly DevSeedOptions _seed;
        private readonly SyntheticTenantSeeder _seeder;
        private readonly TextWriter _output;

        public SeedDevCommand(
            IHostEnvironment environment,
            IOptions<DevSeedOptions> seed,
            SyntheticTenantSeeder seeder,
            TextWriter output) {
            _environment = environment;
            _seed = seed.Value;
            _seeder = seeder;
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
            SyntheticTenantSeeder.ValidatePassword(DevSeedOptions.SectionName, nameof(DevSeedOptions.AdminPassword), _seed.AdminPassword);
            SyntheticTenantSeeder.ValidatePassword(DevSeedOptions.SectionName, nameof(DevSeedOptions.OperatorPasswordTenantA), _seed.OperatorPasswordTenantA);
            SyntheticTenantSeeder.ValidatePassword(DevSeedOptions.SectionName, nameof(DevSeedOptions.OperatorPasswordTenantB), _seed.OperatorPasswordTenantB);

            // Registro no catálogo, atualização das bases e criação dos operadores: o caminho é o do seeder compartilhado.
            return await _seeder.SeedAsync(MigratorApplication.SeedDevVerb, "desenvolvimento",
            [
                new SyntheticTenantPlan(_tenantA,
                [
                    new SyntheticOperator(AdminEmail, ERole.Admin, _seed.AdminPassword),
                    new SyntheticOperator(OperatorEmail, ERole.Operator, _seed.OperatorPasswordTenantA)
                ]),
                new SyntheticTenantPlan(_tenantB,
                [
                    new SyntheticOperator(AdminEmail, ERole.Admin, _seed.AdminPassword),
                    new SyntheticOperator(OperatorEmail, ERole.Operator, _seed.OperatorPasswordTenantB)
                ]),
                new SyntheticTenantPlan(_tenantD,
                [
                    new SyntheticOperator(AdminEmail, ERole.Admin, _seed.AdminPassword)
                ])
            ], cancellationToken);
        }
    }
}
