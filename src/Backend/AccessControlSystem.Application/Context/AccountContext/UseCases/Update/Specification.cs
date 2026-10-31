using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Update {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            var contract = new Contract<Notification>()
                .Requires()
                .IsTrue(request.Id != Guid.Empty, "Id", "Operador não informado")
                .IsFalse(
                    request.Email is null && request.Password is null && request.Role is null,
                    "Request", "Informe ao menos um campo para atualizar");

            if (request.Email is not null) {
                contract.Matches(request.Email, Email.Pattern, "Email", "E-mail inválido");
            }

            if (request.Password is not null) {
                contract.Matches(request.Password, Password.Pattern, "Password", "Senha não atende aos requisitos de segurança");
            }

            if (request.Role is { } role) {
                contract.IsTrue(
                    Enum.IsDefined(role) && role != ERole.None,
                    "Role", "Perfil de acesso inválido");
            }

            return contract;
        }
    }
}
