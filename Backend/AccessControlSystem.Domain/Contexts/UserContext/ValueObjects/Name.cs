using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Domain.SharedContext.ValueObjects;

namespace AccessControlSystem.Domain.Contexts.UserContext.ValueObjects {
    public partial class Name : ValueObject {

        protected Name() {
            
        }
        public Name(string fullName) {

            if (string.IsNullOrWhiteSpace(fullName)) {
                throw new DomainException("Campo nome não pode ser vazio");
            }
            if (fullName.Length < 3) {
                throw new DomainException("Campo nome deve ter pelo menos 3 caracteres");
            }
            if (fullName.Length > 100) {
                throw new DomainException("Campo nome deve ter no máximo 100 caracteres");
            }

            FullName = fullName.Trim();
        }

        public string FullName { get; private set; } = string.Empty;

        public static implicit operator Name(string fullName) {
            return new Name(fullName);
        }
        public static implicit operator string(Name name) {
            return name.ToString();
        }
        public override string ToString() {
            return FullName;
        }
    }
}
