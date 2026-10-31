using AccessControlSystem.Application.SharedContext.UseCases;
using Microsoft.AspNetCore.Diagnostics;

namespace AccessControlSystem.Api.Handlers {
    public class GlobalExceptionHandler : IExceptionHandler {
        private readonly ILogger<GlobalExceptionHandler> _logger;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) {
            _logger = logger;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken) {
            _logger.LogError(exception, "Erro não tratado"); 

            var response = new ErrorResponse("Erro interno do servidor", 500);

            httpContext.Response.StatusCode = 500;
            await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

            return true;
        }
    }
}
