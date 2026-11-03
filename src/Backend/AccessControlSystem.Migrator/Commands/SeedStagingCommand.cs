using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Migrator.Commands {

    /// <summary>
    /// Verbo <c>seed-staging</c> (só em Staging): tenants e operadores FICTÍCIOS de homologação (data-model.md, seção 4;
    /// FR-024 a FR-030). Idempotente. Referência de código: <see cref="SeedDevCommand"/>, de que este comando é o
    /// equivalente de homologação.
    /// </summary>
    /// <remarks>
    /// Cuida só da guarda de ambiente, das senhas e dos dados de homologação. O resto (registrar bases e tenants no
    /// catálogo, atualizar a estrutura das bases com a <c>SeedAdminPolicy</c> e criar os operadores pelo
    /// <c>AppDbContext</c>, sob RLS) é do <see cref="SyntheticTenantSeeder"/>, o mesmo caminho do <c>seed-dev</c>. Os
    /// e-mails ficam num domínio reservado a testes (<c>.test</c>) e <b>nenhum</b> operador usa o e-mail do
    /// administrador fixo. As senhas vêm só de configuração/variável de ambiente e nunca vão à saída.
    /// </remarks>
    public class SeedStagingCommand {

        public const string AdminEmail = "admin@homologacao.test";
        public const string OperatorEmail = "operador@homologacao.test";

        private const string SharedDatabase = "shared_01";
        private const string DedicatedDatabase = "dedicated_01";

        // Dados da seção 4 do data-model.md: ids estáveis de homologação (distintos dos de desenvolvimento) e CNPJs
        // sintéticos INVÁLIDOS de propósito (o catálogo não os valida). Campos de instância, e não estáticos: a regra de
        // não ter estado estático com dado de tenant vale para todo o código.
        private readonly SyntheticTenant _tenantA = new(
            Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001"), "99999999000101", "Tenant A (homologação)", SharedDatabase, EDatabaseKind.Shared);
        private readonly SyntheticTenant _tenantB = new(
            Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002"), "99999999000102", "Tenant B (homologação)", SharedDatabase, EDatabaseKind.Shared);
        private readonly SyntheticTenant _tenantD = new(
            Guid.Parse("aaaaaaaa-0000-4000-8000-000000000003"), "99999999000103", "Tenant D (homologação)", DedicatedDatabase, EDatabaseKind.Dedicated);

        private readonly IHostEnvironment _environment;
        private readonly StagingSeedOptions _seed;
        private readonly SyntheticTenantSeeder _seeder;
        private readonly TextWriter _output;

        public SeedStagingCommand(
            IHostEnvironment environment,
            IOptions<StagingSeedOptions> seed,
            SyntheticTenantSeeder seeder,
            TextWriter output) {
            _environment = environment;
            _seed = seed.Value;
            _seeder = seeder;
            _output = output;
        }

        /// <summary>Devolve o código de saída: 0, 1 (alguma base falhou) ou 5 (recusado fora de Staging).</summary>
        public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default) {

            // A recusa vem antes de qualquer outra coisa: nada é lido, validado nem escrito.
            if (!_environment.IsStaging()) {
                _output.WriteLine(StagingRefusalMessage(_environment.EnvironmentName));
                return ExitCodes.SeedStagingRefused;
            }

            // Senhas inválidas são erro de configuração, e param antes de escrever qualquer coisa. A mensagem nunca traz o valor.
            SyntheticTenantSeeder.ValidatePassword(StagingSeedOptions.SectionName, nameof(StagingSeedOptions.AdminPassword), _seed.AdminPassword);
            SyntheticTenantSeeder.ValidatePassword(StagingSeedOptions.SectionName, nameof(StagingSeedOptions.OperatorPasswordTenantA), _seed.OperatorPasswordTenantA);
            SyntheticTenantSeeder.ValidatePassword(StagingSeedOptions.SectionName, nameof(StagingSeedOptions.OperatorPasswordTenantB), _seed.OperatorPasswordTenantB);

            return await _seeder.SeedAsync(MigratorApplication.SeedStagingVerb, "homologação",
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

        /// <summary>Mensagem da recusa, também usada pela guarda do <see cref="MigratorApplication"/> (antes da configuração).</summary>
        public static string StagingRefusalMessage(string environmentName) =>
            $"seed-staging recusado: o ambiente é '{environmentName}' e ele só roda em Staging. Nada foi escrito.";
    }
}
