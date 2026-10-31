using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Domain.SharedContext.ValueObjects;
using System.Text.RegularExpressions;

namespace AccessControlSystem.Domain.Contexts.UserContext.ValueObjects {
    public partial class Telephone : ValueObject{

        public const string TelephonePattern = @"^\(?\d{2}\)?[\s-]?[\s9]?\d{4}-?\d{4}$";

        protected Telephone() {
            
        }

        public Telephone(string telephoneNumber = "") {

            if (!string.IsNullOrEmpty(telephoneNumber) && !TelephoneRegex().IsMatch(telephoneNumber)) {
                throw new DomainException("Número de telefone inválido");
            }

            TelephoneNumber = telephoneNumber;
        }

        public string TelephoneNumber { get; private set; } = string.Empty;

        public static implicit operator string(Telephone telephone) {
            return telephone.ToString();
        }

        public static implicit operator Telephone(string telephoneNumber) {
            return new Telephone(telephoneNumber);
        }

        public override string ToString() {
            return TelephoneNumber;
        }


        [GeneratedRegex(TelephonePattern)]
        private static partial Regex TelephoneRegex();
    }
}
