using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Services;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Infrastructure.Tenancy {

    public static class TenancyServiceCollectionExtensions {

        /// <summary>
        /// Registra tudo o que a Api precisa para atender vários tenants: opções validadas na partida,
        /// catálogo, resolução do tenant e o <c>AppDbContext</c> apontado para a base do tenant.
        /// Não guarda estado estático nem cache de dado de tenant: tudo é por requisição.
        /// </summary>
        public static IServiceCollection AddTenancy(this IServiceCollection services) {

            #region Opções (validadas na partida)
            services.AddOptions<CatalogOptions>()
                .BindConfiguration(CatalogOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<TenantDatabasesOptions>()
                .BindConfiguration(TenantDatabasesOptions.SectionName)
                .ValidateOnStart();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TenantDatabasesOptions>, TenantDatabasesOptionsValidator>());
            #endregion

            #region Catálogo
            services.AddDbContext<CatalogDbContext>((provider, options) =>
                options.UseNpgsql(provider.GetRequiredService<IOptions<CatalogOptions>>().Value.Catalog));

            services.AddScoped<ITenantCatalog, TenantCatalog>();
            #endregion

            #region Tenant da requisição
            services.AddScoped<TenantScope>();
            services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<TenantScope>());
            services.AddSingleton<TenantDatabaseConnectionProvider>();
            services.AddScoped<ITenantResolver, TenantResolver>();
            #endregion

            #region Dados do tenant
            // Falha fechada: se algo pedir o AppDbContext antes de o tenant ser resolvido, lança. Nunca
            // há uma base "padrão" para onde cair.
            services.AddDbContext<AppDbContext>((provider, options) => {
                var scope = provider.GetRequiredService<TenantScope>();

                if (!scope.IsResolved) {
                    throw new InvalidOperationException(
                        "O AppDbContext só pode ser usado depois de o tenant da requisição ser resolvido.");
                }

                var connectionString = provider
                    .GetRequiredService<TenantDatabaseConnectionProvider>()
                    .GetConnectionString(scope.DatabaseName);

                TenantDbContextOptions.Configure(options, connectionString);
            });
            #endregion

            return services;
        }
    }
}
