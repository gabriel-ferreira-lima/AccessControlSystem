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

        /// <summary>
        /// Versão de segurança do operador. Todo token carrega a versão do momento do login e só vale enquanto ela
        /// for igual à atual: trocar a senha, mudar o perfil ou desativar o operador a incrementa e, com isso,
        /// derruba os tokens emitidos antes. Reativar e trocar só o e-mail NÃO a alteram.
        /// </summary>
        public int SecurityVersion { get; private set; }

        public void UpdateEmail(Email email) {
            Email = email;
        }

        /// <summary>Senha informada conta sempre como troca (mesmo igual à atual): derruba os tokens anteriores.</summary>
        public void UpdatePassword(Password password) {
            Password = password;
            SecurityVersion++;
        }

        /// <summary>Só um perfil diferente do atual muda o operador e derruba os tokens anteriores.</summary>
        public void UpdateRole(ERole role) {
            if (Role == role) {
                return;
            }

            Role = role;
            SecurityVersion++;
        }

        /// <summary>
        /// Desativar derruba os tokens anteriores, e reativar não os revalida: a versão muda na transição
        /// ativo para inativo. Desativar quem já está inativo não faz nada.
        /// </summary>
        public void Deactivate() {
            if (!IsActive) {
                return;
            }

            IsActive = false;
            SecurityVersion++;
        }

        public void Activate() {
            IsActive = true;
        }
    }
}
