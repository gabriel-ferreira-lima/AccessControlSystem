namespace AccessControlSystem.Application.SharedContext.Tenancy {

    /// <summary>
    /// Leitura do catálogo de tenants. É o único caminho do código de tenant até o catálogo, que
    /// pertence ao sistema de provisionamento: aqui ele só é lido e devolve <c>record</c>s.
    /// </summary>
    public interface ITenantCatalog {

        /// <summary>Tenant e a base em que ele mora, em uma única consulta. <c>null</c> se não existe.</summary>
        Task<TenantInfo?> GetTenantByIdAsync(Guid tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Tenants ativos, paginados por chave (<c>id &gt; afterTenantId</c>) e ordenados por id.
        /// <paramref name="size"/> vai de 1 a 100; fora disso lança <see cref="ArgumentOutOfRangeException"/>.
        /// </summary>
        Task<IReadOnlyList<TenantInfo>> GetActiveTenantListAsync(
            Guid? afterTenantId, int size, CancellationToken cancellationToken = default);
    }
}
