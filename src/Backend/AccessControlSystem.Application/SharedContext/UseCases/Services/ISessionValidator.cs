namespace AccessControlSystem.Application.SharedContext.UseCases.Services {

    /// <summary>
    /// Confere, a cada requisição autenticada, se a sessão do token ainda vale: operador existente, ativo e com a
    /// versão de segurança do token (não basta olhar a situação do operador).
    /// </summary>
    public interface ISessionValidator {

        /// <returns>
        /// <c>true</c> se o operador existe no tenant corrente, está ativo e tem exatamente esta versão de segurança
        /// (a do token). Uma única consulta ao banco.
        /// </returns>
        Task<bool> IsValidAsync(Guid operatorId, int securityVersion, CancellationToken cancellationToken = default);
    }
}
