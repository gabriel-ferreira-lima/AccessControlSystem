using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Entities;

namespace AccessControlSystem.Domain.Contexts.AccountContext.Entities {
    public class Operator : Entity{

        protected Operator() {
            
        }

        public Operator(Email email, Password password, ERole role) {
            Email = email;
            Password = password;
            Role = role;
        }

        public Email Email { get; private set; } = null!;
        public Password Password { get; private set; } = null!;
        public ERole Role { get; private set; }
        public bool IsActive { get; private set; } = true;

        public void UpdateEmail(Email email) {
            Email = email;
        }

        public void UpdatePassword(Password password) {
            Password = password;
        }

        public void UpdateRole(ERole role) {
            Role = role;
        }

        public void Deactivate() {
            IsActive = false;
        }

        public void Activate() {
            IsActive = true;
        }
    }
}
