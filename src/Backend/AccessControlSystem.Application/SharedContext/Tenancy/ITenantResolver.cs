namespace AccessControlSystem.Application.SharedContext.Tenancy {

    /// <summary>Valida o tenant no catálogo e o torna o tenant corrente da requisição.</summary>
    public interface ITenantResolver {

        /// <returns>
        /// <c>true</c> se o tenant existe e está ativo (o contexto fica resolvido); <c>false</c> se não
        /// existe ou está inativo, sem distinguir os dois casos. Lança se a base do tenant não tem
        /// connection string configurada (erro de configuração: nunca cai em outra base).
        /// </returns>
        Task<bool> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default);
    }
}
