using AccessControlSystem.Application.SharedContext.Tenancy;

namespace AccessControlSystem.Infrastructure.Data {

    /// <summary>
    /// Contexto do EF que conhece o tenant da requisição. Os interceptors de tenant são instâncias
    /// únicas e sem estado, então descobrem o tenant a partir do contexto em uso
    /// (<c>eventData.Context</c>) em vez de capturá-lo no construtor.
    /// </summary>
    public interface ITenantAwareDbContext {
        ITenantContext TenantContext { get; }
    }
}
