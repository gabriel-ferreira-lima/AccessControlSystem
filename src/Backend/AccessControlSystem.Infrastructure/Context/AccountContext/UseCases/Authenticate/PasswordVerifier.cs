using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using System.Security.Cryptography;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate {

    public class PasswordVerifier : IPasswordVerifier {

        // Hash fictício: criado pelo mesmo Password (mesmo SecureIdentity.PasswordHasher) que cria os hashes reais, logo com o
        // mesmo algoritmo e o mesmo custo. É CAMPO DE INSTÂNCIA de um singleton, nunca estático (NoStaticTenantStateTests),
        // e não guarda dado de tenant. O texto é aleatório e descartado: nenhuma senha de operador confere com ele.
        private readonly Password _placeholder = new("Aa1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

        /// <summary>Hash fictício usado quando o operador não existe (exposto para o teste de estrutura).</summary>
        public Password Placeholder => _placeholder;

        public bool Verify(Operator? @operator, string password) {
            var result = (@operator?.Password ?? _placeholder).Verify(password);

            return @operator is not null && result;
        }
    }
}
