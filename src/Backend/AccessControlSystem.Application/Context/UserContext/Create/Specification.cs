using AccessControlSystem.Application.SharedContext.UseCases.Validation;
using AccessControlSystem.Domain.Contexts.UserContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;


namespace AccessControlSystem.Application.Context.UserContext.Create {
    public static class Specification {

        // Sem value object para a observação: o limite mora aqui.
        private const int ObservationMaxLength = 100;

        public static Contract<Notification> Ensure(Request request) {
            // Tamanho primeiro: entrada gigante é recusada antes de qualquer regex ou validação de CPF.
            var sizes = new Contract<Notification>()
                .Requires()
                .IsMaxLength(request.Name, Name.MaxLength, "Name", "Campo nome deve ter no máximo 100 caracteres")
                .IsMaxLength(request.Observation, ObservationMaxLength, "Observation", "Campo observação deve ter no máximo 100 caracteres")
                .IsMaxLength(request.Telephone, Telephone.MaxLength, "Telephone", "Telefone deve ter no máximo 20 caracteres")
                .IsMaxLength(request.Rg, Rg.MaxLength, "Rg", "RG deve ter no máximo 20 caracteres")
                .IsMaxLength(request.Cpf, Cpf.MaxLength, "Cpf", "CPF deve ter no máximo 14 caracteres")
                .IsMaxLength(request.Password, PasswordAccess.MaxLength, "Password", "Senha deve ter no máximo 6 caracteres");

            if (!sizes.IsValid) {
                return sizes;
            }

            var contract = new Contract<Notification>()
                .Requires()
                .IsFalse(string.IsNullOrWhiteSpace(request.Name), "Name", "Campo nome não pode ser vazio")
                .IsGreaterThan((request.Name ?? string.Empty).Length, 2, "Name", "Campo nome deve ter pelo menos 3 caracteres")
                .IsFalse(request.DateStart.HasValue && request.DateEnd.HasValue && request.DateEnd < request.DateStart,
                    "DateEnd", "Data final não pode ser anterior à data inicial");

            if (!string.IsNullOrEmpty(request.Telephone)) {
                contract.Matches(request.Telephone, Telephone.TelephonePattern, "Telephone", "Telefone inválido");
            }

            if (!string.IsNullOrEmpty(request.Password)) {
                contract.Matches(request.Password, PasswordAccess.Pattern, "Password", "Senha deve conter apenas números e ter entre 4 e 6 dígitos");
            }

            if (!string.IsNullOrEmpty(request.Rg)) {
                contract.Matches(request.Rg, Rg.RgPattern, "Rg", "RG inválido");
            }

            if (!string.IsNullOrEmpty(request.Cpf)) {
                contract.IsTrue(Cpf.IsValid(request.Cpf), "Cpf", "CPF inválido");
            }

            return contract;
        }
    }
}
