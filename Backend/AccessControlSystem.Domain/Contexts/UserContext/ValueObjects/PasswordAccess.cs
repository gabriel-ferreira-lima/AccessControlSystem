using System.Text.RegularExpressions;
using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Domain.SharedContext.ValueObjects;

namespace AccessControlSystem.Domain.Contexts.UserContext.ValueObjects {
    public partial class PasswordAccess : ValueObject{

        public const string Pattern = @"^\d{4,6}$";

        protected PasswordAccess() {
            
        }

        public PasswordAccess(string value = "") {
            if (!string.IsNullOrEmpty(value) && !PasswordRegex().IsMatch(value)) {
                throw new DomainException("Senha inválida. A senha deve conter apenas números e ter entre 4 e 6 dígitos.");
            }

            Value = value;
        }

        public string Value { get; private set; } = string.Empty;


        [GeneratedRegex(Pattern)]
        private static partial Regex PasswordRegex();
    }
}
