using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts {

    /// <summary>
    /// Verificação de senha do login (FR-014). Existe como costura para que o tempo de resposta não revele quais
    /// e-mails estão cadastrados e para que o handler prove, em teste, que a verificação roda em todos os casos.
    /// </summary>
    public interface IPasswordVerifier {

        /// <summary>
        /// Sempre executa <b>uma</b> verificação de hash. Com operador existente (ativo ou não), verifica contra o hash
        /// dele; com <c>null</c>, verifica contra um hash fictício de mesmo algoritmo e custo e devolve <c>false</c>.
        /// Não decide sobre conta inativa: isso é do handler, depois da verificação.
        /// </summary>
        bool Verify(Operator? @operator, string password);
    }
}
