using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using Flunt.Notifications;

namespace AccessControlSystem.Application.SharedContext.UseCases {
    public abstract class Response : IResponse {

        public Response(string? message, int status) {
            Message = message ?? string.Empty;
            Status = status;
        }
        public string Message { get; private set; }
        public int Status { get; private set; }
        public bool IsSuccess {
            get {
                if (Status >= 200 && Status <= 299) {
                    return true;
                }

                return false;
            }
        }
        public IEnumerable<Notification>? Notifications { get; set; }
    }
}
