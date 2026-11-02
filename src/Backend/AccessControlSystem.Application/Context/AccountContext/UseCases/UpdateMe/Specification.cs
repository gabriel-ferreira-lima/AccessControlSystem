using AccessControlSystem.Application.SharedContext.UseCases.Validation;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            // Tamanho primeiro (só dos campos informados): entrada gigante é recusada antes de qualquer regex.
            var sizes = new Contract<Notification>()
                .Requires()
                .IsMaxLength(request.Email, Email.MaxLength, "Email", "E-mail deve ter no máximo 255 caracteres")
                .IsMaxLength(request.Password, Password.MaxLength, "Password", "Senha deve ter no máximo 128 caracteres");

            if (!sizes.IsValid) {
                return sizes;
            }

            var contract = new Contract<Notification>()
                .Requires()
                .IsTrue(
                    request.Id != Guid.Empty,
                    "Id",
                    "Operador não informado"
                )
                .IsFalse(
                    request.Email is null && request.Password is null,
                    "Request",
                    "Informe ao menos um campo para atualizar"
                );

            if (request.Email is not null) {
                contract.Matches(request.Email, Email.Pattern, "Email", "E-mail inválido");
            }

            if (request.Password is not null) {
                contract.Matches(request.Password, Password.Pattern, "Password", "Senha não atende aos requisitos de segurança");
            }

            return contract;
        }
    }
}
