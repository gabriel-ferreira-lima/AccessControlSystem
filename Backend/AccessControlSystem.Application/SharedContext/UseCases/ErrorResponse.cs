using Flunt.Notifications;

namespace AccessControlSystem.Application.SharedContext.UseCases {
    public class ErrorResponse : Response {
        public ErrorResponse(string message, int status, IEnumerable<Notification>? notifications = null)
            : base(message, status) {
            Notifications = notifications;
        }
    }
}
