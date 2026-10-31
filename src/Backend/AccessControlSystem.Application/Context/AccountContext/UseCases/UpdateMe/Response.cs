using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using Flunt.Notifications;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe {
    public class Response : SharedContext.UseCases.Response {

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

    public record ResponseData(Guid Id, string Email);
}
