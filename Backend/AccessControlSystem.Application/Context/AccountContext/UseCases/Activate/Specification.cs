using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Activate {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            return new Contract<Notification>()
                .Requires()
                .IsTrue(request.Id != Guid.Empty, "Id", "Operador não informado");
        }
    }
}
