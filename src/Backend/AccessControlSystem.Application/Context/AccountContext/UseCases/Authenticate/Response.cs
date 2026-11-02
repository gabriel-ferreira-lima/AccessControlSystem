using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using Flunt.Notifications;
using System.Text.Json.Serialization;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate {
    public class Response : SharedContext.UseCases.Response, IResponse, IRetryAfterResponse {

        protected Response() : base(string.Empty, 400) {

        }

        public Response(string message, int status, IEnumerable<Notification>? notifications = null) : base(message, status) {
            Notifications = notifications;
        }

        public Response(string message, int status, ResponseData data) : base(message, status) {
            Notifications = null;
            Data = data;

        }

        public ResponseData? Data { get; set; }

        /// <summary>Quanto esperar antes de tentar de novo (bloqueio por conta). Vira o cabeçalho <c>Retry-After</c>; não vai no corpo.</summary>
        [JsonIgnore]
        public TimeSpan? RetryAfter { get; init; }

        /// <summary>
        /// Bloqueio por conta: a mesma mensagem do limite por origem (para não revelar nada sobre a conta), com o tempo restante.
        /// </summary>
        public static Response TooManyAttempts(TimeSpan retryAfter) =>
            new("Muitas tentativas. Tente novamente em instantes.", 429) { RetryAfter = retryAfter };
    }

    public class ResponseData {
        public string Token { get; set; } = string.Empty;
        public Guid Id { get; set; } = Guid.Empty;
        public string Email { get; set; } = string.Empty;
        public ERole Role { get; set; }
    }

}
