namespace AccessControlSystem.Application.SharedContext.Tenancy {

    /// <summary>
    /// Tenant da requisição corrente. É preenchido uma única vez por requisição, a partir do
    /// token (ou do tenant padrão do login) validado contra o catálogo.
    /// </summary>
    public interface ITenantContext {

        /// <summary>Indica se o tenant da requisição já foi resolvido.</summary>
        bool IsResolved { get; }

        /// <summary>
        /// Identificador do tenant. Lança <see cref="InvalidOperationException"/> quando ainda não
        /// foi resolvido (falha fechada: nunca assume outro tenant).
        /// </summary>
        Guid TenantId { get; }
    }
}
