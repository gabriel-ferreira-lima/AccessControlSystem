using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using AccessControlSystem.Migrator.Commands;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Migrator {

    /// <summary>Códigos de saída do Migrator (contracts/migrator-cli.md).</summary>
    public static class ExitCodes {
        public const int Ok = 0;
        public const int SomeDatabaseFailed = 1;
        public const int InvalidConfiguration = 2;
        public const int SeedDevRefused = 3;
        public const int LockTimeout = 4;
    }

    /// <summary>
    /// Ponto de entrada testável do Migrator. O <c>Program.cs</c> só delega a esta classe, que roteia
    /// os verbos, compõe o host e traduz o resultado em código de saída.
    /// </summary>
    public static class MigratorApplication {

        public const string MigrateVerb = "migrate";
        public const string SeedDevVerb = "seed-dev";

        /// <param name="configure">Permite aos testes injetar configuração (vem depois das variáveis de ambiente).</param>
        /// <param name="output">Onde o resumo e as mensagens saem (padrão: o console).</param>
        public static async Task<int> RunAsync(
            string[] args,
            Action<IConfigurationBuilder>? configure = null,
            CancellationToken cancellationToken = default,
            TextWriter? output = null) {

            output ??= Console.Out;

            if (args.Length != 1 || (args[0] != MigrateVerb && args[0] != SeedDevVerb)) {
                output.WriteLine($"Uso: AccessControlSystem.Migrator {MigrateVerb} | {SeedDevVerb}");
                return ExitCodes.InvalidConfiguration;
            }

            var verb = args[0];

            var configuration = new ConfigurationManager();
            configuration.AddEnvironmentVariables();
            configure?.Invoke(configuration);

            // DOTNET_ENVIRONMENT decide o ambiente; sem ele vale Production (o mais restritivo).
            var environment = configuration["DOTNET_ENVIRONMENT"];
            var environmentName = string.IsNullOrWhiteSpace(environment) ? Environments.Production : environment;

            // O seed-dev é recusado antes de qualquer outra coisa (nem a configuração é lida).
            if (verb == SeedDevVerb && !string.Equals(environmentName, Environments.Development, StringComparison.OrdinalIgnoreCase)) {
                output.WriteLine($"seed-dev recusado: o ambiente é '{environmentName}' e ele só roda em Development. Nada foi escrito.");
                return ExitCodes.SeedDevRefused;
            }

            var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings {
                Configuration = configuration,
                EnvironmentName = environmentName
            });

            AddServices(builder.Services, output, verb);

            using var host = builder.Build();

            try {
                // Valida as opções (ValidateOnStart) antes de tocar em qualquer banco.
                await host.StartAsync(cancellationToken);
            }
            catch (OptionsValidationException exception) {
                output.WriteLine("Configuração inválida:");
                foreach (var failure in exception.Failures) {
                    output.WriteLine($"  - {failure}");
                }
                return ExitCodes.InvalidConfiguration;
            }

            try {
                return verb == SeedDevVerb
                    ? await host.Services.GetRequiredService<SeedDevCommand>().ExecuteAsync(cancellationToken)
                    : await host.Services.GetRequiredService<MigrateCommand>().ExecuteAsync(cancellationToken);
            }
            catch (MigratorLockTimeoutException exception) {
                output.WriteLine(exception.Message);
                return ExitCodes.LockTimeout;
            }
            catch (ArgumentException exception) {
                // Configuração de InitialTenant incompleta.
                output.WriteLine($"Configuração inválida: {exception.Message}");
                return ExitCodes.InvalidConfiguration;
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                output.WriteLine($"Erro inesperado: {ErrorSanitizer.FromException(exception, KnownConnectionStrings(host.Services))}");
                return ExitCodes.SomeDatabaseFailed;
            }
            finally {
                await host.StopAsync(CancellationToken.None);
            }
        }

        private static void AddServices(IServiceCollection services, TextWriter output, string verb) {
            services.AddOptions<MigratorOptions>()
                .BindConfiguration(MigratorOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<CatalogMigrationOptions>()
                .BindConfiguration(CatalogMigrationOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<TenantDatabasesOptions>().BindConfiguration(TenantDatabasesOptions.SectionName);
            services.AddOptions<InitialTenantOptions>().BindConfiguration(InitialTenantOptions.SectionName);

            services.AddSingleton(provider => new DbContextOptionsBuilder<CatalogDbContext>()
                .UseNpgsql(
                    provider.GetRequiredService<IOptions<CatalogMigrationOptions>>().Value.CatalogMigration,
                    npgsql => npgsql.MigrationsAssembly("AccessControlSystem.Api"))
                .Options);

            services.AddSingleton<IDatabaseMigrationRunner, EfDatabaseMigrationRunner>();
            services.AddSingleton<CatalogRegistry>();
            services.AddSingleton<InitialTenantRegistrar>();
            services.AddSingleton<MigrationOrchestrator>();
            services.AddSingleton(output);
            services.AddSingleton<MigrateCommand>();

            if (verb == SeedDevVerb) {
                // As senhas de desenvolvimento só são exigidas por quem vai usá-las.
                services.AddOptions<DevSeedOptions>()
                    .BindConfiguration(DevSeedOptions.SectionName)
                    .ValidateDataAnnotations()
                    .ValidateOnStart();
                services.AddSingleton<SeedDevCommand>();
            }
        }

        // Todas as connection strings conhecidas, para nenhuma delas aparecer numa mensagem de erro.
        private static string?[] KnownConnectionStrings(IServiceProvider services) {
            var catalog = services.GetRequiredService<IOptions<CatalogMigrationOptions>>().Value.CatalogMigration;
            var databases = services.GetRequiredService<IOptions<TenantDatabasesOptions>>().Value.Values
                .SelectMany(x => new[] { x.ConnectionString, x.MigrationConnectionString });

            return databases.Append(catalog).ToArray();
        }
    }
}
