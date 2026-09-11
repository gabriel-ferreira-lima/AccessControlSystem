using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Create {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            return new Contract<Notification>()
                .Requires()
                .Matches(request.Email, Email.Pattern, "Email", "E-mail inválido")
                .Matches(request.Password, Password.Pattern, "Password", "Senha não atende aos requisitos de segurança")
                .IsTrue(Enum.IsDefined(request.Role) && request.Role != ERole.None, "Role", "Perfil de acesso inválido");
        }
    }
}
