using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            return new Contract<Notification>()
                .Requires()
                .Matches(request.Email, Email.Pattern, "Email", "E-mail inválido");
        }
    }
}
