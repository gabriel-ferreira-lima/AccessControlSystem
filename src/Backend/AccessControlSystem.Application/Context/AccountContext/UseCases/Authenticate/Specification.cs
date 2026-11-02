using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            return new Contract<Notification>()
                .Requires()
                // Espaços nas pontas não invalidam: o handler normaliza o e-mail (Email VO) antes de usá-lo.
                .Matches(request.Email?.Trim(), Email.Pattern, "Email", "E-mail inválido");
        }
    }
}
