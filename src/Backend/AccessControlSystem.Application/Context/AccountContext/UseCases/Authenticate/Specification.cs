using AccessControlSystem.Application.SharedContext.UseCases.Validation;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            // Tamanho primeiro, sobre o texto como recebido (antes de qualquer Trim): evita regex e hash em entrada gigante.
            // A senha só tem o limite; o login nunca valida a complexidade dela.
            var contract = new Contract<Notification>()
                .Requires()
                .IsMaxLength(request.Email, Email.MaxLength, "Email", "E-mail deve ter no máximo 255 caracteres")
                .IsMaxLength(request.Password, Password.MaxLength, "Password", "Senha deve ter no máximo 128 caracteres");

            if (!contract.IsValid) {
                return contract;
            }

            // Espaços nas pontas não invalidam: o handler normaliza o e-mail (Email VO) antes de usá-lo.
            return contract.Matches(request.Email?.Trim(), Email.Pattern, "Email", "E-mail inválido");
        }
    }
}
