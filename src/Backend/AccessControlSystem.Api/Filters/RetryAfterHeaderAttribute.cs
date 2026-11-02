using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Globalization;

namespace AccessControlSystem.Api.Filters {

    /// <summary>
    /// Escreve o cabeçalho <c>Retry-After</c> (em segundos, arredondado para cima e no mínimo 1) quando a resposta do use case
    /// informa quanto esperar (<see cref="IRetryAfterResponse"/>), como no bloqueio de login por conta. Mantém o controller
    /// fino: ele continua só devolvendo <c>StatusCode(result.Status, result)</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RetryAfterHeaderAttribute : ResultFilterAttribute {

        public override void OnResultExecuting(ResultExecutingContext context) {
            if (context.Result is ObjectResult { Value: IRetryAfterResponse { RetryAfter: { } wait } }) {
                var seconds = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            }
        }
    }
}
