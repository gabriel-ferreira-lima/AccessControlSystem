using AccessControlSystem.Application.SharedContext.UseCases.Validation;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Create {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            // Tamanho primeiro: entrada gigante é recusada antes de qualquer regex.
            var sizes = new Contract<Notification>()
                .Requires()
                .IsMaxLength(request.Email, Email.MaxLength, "Email", "E-mail deve ter no máximo 255 caracteres")
                .IsMaxLength(request.Password, Password.MaxLength, "Password", "Senha deve ter no máximo 128 caracteres");

            if (!sizes.IsValid) {
                return sizes;
            }

            return new Contract<Notification>()
                .Requires()
                .Matches(request.Email, Email.Pattern, "Email", "E-mail inválido")
                .Matches(request.Password, Password.Pattern, "Password", "Senha não atende aos requisitos de segurança")
                .IsTrue(Enum.IsDefined(request.Role) && request.Role != ERole.None, "Role", "Perfil de acesso inválido");
        }
    }
}
