using AccessControlSystem.Domain.Contexts.UserContext.ValueObjects;
using Flunt.Notifications;
using Flunt.Validations;


namespace AccessControlSystem.Application.Context.UserContext.Create {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            var contract = new Contract<Notification>()
                .Requires()
                .IsFalse(string.IsNullOrWhiteSpace(request.Name), "Name", "Campo nome não pode ser vazio")
                .IsGreaterThan((request.Name ?? string.Empty).Length, 2, "Name", "Campo nome deve ter pelo menos 3 caracteres")
                .IsLowerThan((request.Name ?? string.Empty).Length, 101, "Name", "Campo nome deve ter no máximo 100 caracteres")
                .IsLowerThan((request.Observation ?? string.Empty).Length, 101, "Observation", "Campo observação deve ter no máximo 100 caracteres")
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
