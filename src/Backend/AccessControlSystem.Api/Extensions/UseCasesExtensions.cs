using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AccessControlSystem.Api.Extensions {
    public static class UseCasesExtensions {

        public static void AddAccountContext(this WebApplicationBuilder builder) {
            #region Create
            builder.Services.AddScoped<
                Application.Context.AccountContext.UseCases.Create.Contracts.IRepository,
                Infrastructure.Context.AccountContext.UseCases.Create.Repository>();

            builder.Services.AddScoped<
                IHandler<
                    Application.Context.AccountContext.UseCases.Create.Request,
                    Application.Context.AccountContext.UseCases.Create.Response>,
                    Application.Context.AccountContext.UseCases.Create.Handler>();
            #endregion

            #region Authenticate
            builder.Services.AddScoped<
                Application.Context.AccountContext.UseCases.Authenticate.Contracts.IRepository,
                Infrastructure.Context.AccountContext.UseCases.Authenticate.Repository>();

            builder.Services.AddScoped<
                Application.SharedContext.UseCases.Services.ITokenService,
                Api.Services.AccountContext.TokenService>();

            // Singleton: só guarda o hash fictício (campo de instância), sem dado de tenant.
            builder.Services.AddSingleton<
                Application.Context.AccountContext.UseCases.Authenticate.Contracts.IPasswordVerifier,
                Infrastructure.Context.AccountContext.UseCases.Authenticate.PasswordVerifier>();

            // Limite de login por conta, em memória da instância (violação temporária dos Princípios I e XIII; a spec 06
            // troca por Redis no mesmo contrato). Singleton: o registro tem de ser um só por instância.
            builder.Services.AddSingleton<
                Application.Context.AccountContext.UseCases.Authenticate.Contracts.ILoginThrottle,
                Infrastructure.Context.AccountContext.UseCases.Authenticate.InMemoryLoginThrottle>();
            builder.Services.TryAddSingleton(TimeProvider.System);
            builder.Services.AddOptions<Infrastructure.Context.AccountContext.UseCases.Authenticate.LoginThrottleOptions>()
                .BindConfiguration(Infrastructure.Context.AccountContext.UseCases.Authenticate.LoginThrottleOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            builder.Services.AddScoped<
                IHandler<
                    Application.Context.AccountContext.UseCases.Authenticate.Request,
                    Application.Context.AccountContext.UseCases.Authenticate.Response>,
                    Application.Context.AccountContext.UseCases.Authenticate.Handler>();
            #endregion

            #region Update
            builder.Services.AddScoped<
                Application.Context.AccountContext.UseCases.Update.Contracts.IRepository,
                Infrastructure.Context.AccountContext.UseCases.Update.Repository>();

            builder.Services.AddScoped<
                IHandler<
                    Application.Context.AccountContext.UseCases.Update.Request,
                    Application.Context.AccountContext.UseCases.Update.Response>,
                    Application.Context.AccountContext.UseCases.Update.Handler>();
            #endregion

            #region Get
            builder.Services.AddScoped<
                Application.Context.AccountContext.UseCases.Get.Contracts.IRepository,
                Infrastructure.Context.AccountContext.UseCases.Get.Repository>();

            builder.Services.AddScoped<
                IHandler<
                    Application.Context.AccountContext.UseCases.Get.Request, 
                    Application.Context.AccountContext.UseCases.Get.Response>,
                    Application.Context.AccountContext.UseCases.Get.Handler>();
            #endregion

            #region UpdateMe
            builder.Services.AddScoped<
                Application.Context.AccountContext.UseCases.UpdateMe.Contracts.IRepository,
                Infrastructure.Context.AccountContext.UseCases.UpdateMe.Repository>();

            builder.Services.AddScoped<
                IHandler<
                    Application.Context.AccountContext.UseCases.UpdateMe.Request,
                    Application.Context.AccountContext.UseCases.UpdateMe.Response>,
                    Application.Context.AccountContext.UseCases.UpdateMe.Handler>();
            #endregion

            #region GetMe
            builder.Services.AddScoped<
                Application.Context.AccountContext.UseCases.GetMe.Contracts.IRepository,
                Infrastructure.Context.AccountContext.UseCases.GetMe.Repository>();

            builder.Services.AddScoped<
                IHandler<
                    Application.Context.AccountContext.UseCases.GetMe.Request,
                    Application.Context.AccountContext.UseCases.GetMe.Response>,
                    Application.Context.AccountContext.UseCases.GetMe.Handler>();
            #endregion

            #region Deactivate
            builder.Services.AddScoped<
                Application.Context.AccountContext.UseCases.Deactivate.Contracts.IRepository,
                Infrastructure.Context.AccountContext.UseCases.Deactivate.Repository>();

            builder.Services.AddScoped<
                IHandler<
                    Application.Context.AccountContext.UseCases.Deactivate.Request,
                    Application.Context.AccountContext.UseCases.Deactivate.Response>,
                    Application.Context.AccountContext.UseCases.Deactivate.Handler>();
            #endregion

            #region Activate
            builder.Services.AddScoped<
                Application.Context.AccountContext.UseCases.Activate.Contracts.IRepository,
                Infrastructure.Context.AccountContext.UseCases.Activate.Repository>();

            builder.Services.AddScoped<
                IHandler<
                    Application.Context.AccountContext.UseCases.Activate.Request,
                    Application.Context.AccountContext.UseCases.Activate.Response>,
                    Application.Context.AccountContext.UseCases.Activate.Handler>();
            #endregion
        }
    }
}
