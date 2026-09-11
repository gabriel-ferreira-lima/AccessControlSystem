using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Domain.SharedContext.ValueObjects;
using SecureIdentity.Password;
using System.Text.RegularExpressions;

namespace AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects {

    public partial class Password : ValueObject{
        public const string Pattern = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z0-9]).{8,}$";

        protected Password() {
            
        }

        public Password(string text) {

            if (!PasswordRegex().IsMatch(text)) {
                throw new DomainException("Senha não atende aos requisitos de segurança");
            }

            Hash = PasswordHasher.Hash(text);
        }

        public string Hash { get; private set; } = null!;

        public bool Verify(string password) {
            return PasswordHasher.Verify(Hash, password);
        }


        [GeneratedRegex(Pattern)]
        private static partial Regex PasswordRegex();
    }
}
