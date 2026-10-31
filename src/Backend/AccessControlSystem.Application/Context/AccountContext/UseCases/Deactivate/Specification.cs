using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Deactivate {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            return new Contract<Notification>()
                .Requires()
                .IsTrue(request.Id != Guid.Empty, "Id", "Operador não informado")
                .IsFalse(request.Id == request.RequestedBy, "Id", "Não é possível desativar a própria conta");
        }
    }
}
