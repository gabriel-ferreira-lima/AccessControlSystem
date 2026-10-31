using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using Flunt.Notifications;


namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Get {
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

    public record ResponseData(
        IReadOnlyList<OperatorItem> Operators,
        int Page,
        int Size,
        int Total,
        int TotalPages);

    public record OperatorItem(Guid Id, string Email, ERole Role, bool IsActive);
}
