using AccessControlSystem.Api.Options;
using AccessControlSystem.Application.SharedContext.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Api.Filters {

    /// <summary>
    /// Marca o endpoint de login: ele não autentica por token e resolve o tenant padrão da
    /// configuração (<c>Login:DefaultTenantId</c>) antes do model binding. Existe só até a spec do
    /// login com escolha de tenant.
    /// </summary>
    /// <remarks>
    /// É um filtro de recurso (roda antes do model binding e do handler). Tenant padrão ausente,
    /// inexistente ou inativo responde com a mesma recusa de credenciais inválidas de hoje, sem
    /// revelar o motivo; o motivo vai só para o log, sem tenant, token, e-mail ou segredo.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class UseDefaultLoginTenantAttribute : Attribute, IAsyncResourceFilter {

        public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next) {
            var services = context.HttpContext.RequestServices;
            var logger = services.GetRequiredService<ILogger<UseDefaultLoginTenantAttribute>>();
            var defaultTenantId = services.GetRequiredService<IOptions<LoginOptions>>().Value.DefaultTenantId;

            if (!defaultTenantId.HasValue || defaultTenantId.Value == Guid.Empty) {
                logger.LogWarning("Login recusado: Login:DefaultTenantId não está configurado.");
                context.Result = InvalidCredentials();
                return;
            }

            var resolver = services.GetRequiredService<ITenantResolver>();

            if (!await resolver.ResolveAsync(defaultTenantId.Value, context.HttpContext.RequestAborted)) {
                logger.LogWarning("Login recusado: o tenant configurado em Login:DefaultTenantId não existe ou está inativo.");
                context.Result = InvalidCredentials();
                return;
            }

            await next();
        }

        private static ObjectResult InvalidCredentials() {
            var response = new Application.Context.AccountContext.UseCases.Authenticate.Response("Credenciais inválidas", 401);
            return new ObjectResult(response) { StatusCode = response.Status };
        }
    }
}
