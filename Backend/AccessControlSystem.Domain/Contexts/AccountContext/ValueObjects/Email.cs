using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Domain.SharedContext.ValueObjects;
using System.Text.RegularExpressions;

namespace AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects {
    public partial class Email : ValueObject {
        public const string Pattern = @"^\w+([-+.']\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$";

        protected Email() {
            
        }

        public Email(string address) {
            
            if (string.IsNullOrEmpty(address))
                throw new DomainException("E-mail inválido");

            Address = address.Trim().ToLowerInvariant();

            if (Address.Length < 5) {
                throw new DomainException("E-mail inválido");
            }

            if (!EmailRegex().IsMatch(Address)) {
                throw new DomainException("E-mail inválido");
            }
        }

        public string Address { get; } = null!;


        public static implicit operator string(Email email) {
            return email.ToString();
        }

        public static implicit operator Email(string address) {
            return new Email(address);
        }

        public override string ToString() {
            return Address;
        }


        [GeneratedRegex(Pattern)]
        private static partial Regex EmailRegex();
    }
}
