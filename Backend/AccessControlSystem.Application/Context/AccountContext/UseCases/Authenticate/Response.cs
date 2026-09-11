using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using Flunt.Notifications;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate {
    public class Response : SharedContext.UseCases.Response, IResponse {

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
    }

    public class ResponseData {
        public string Token { get; set; } = string.Empty;
        public Guid Id { get; set; } = Guid.Empty;
        public string Email { get; set; } = string.Empty;
        public ERole Role { get; set; }
    }

}
