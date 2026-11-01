using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Data.Interceptors {

    /// <summary>
    /// Descobre o tenant a partir do contexto em uso. Os interceptors de tenant são instâncias únicas
    /// e sem estado, então o tenant nunca fica guardado neles (Princípio I).
    /// </summary>
    internal static class TenantInterceptorSupport {

        /// <summary>
        /// Devolve o tenant do contexto. Falha fechada: se o contexto não informa tenant ou o tenant
        /// ainda não foi resolvido, lança antes de qualquer comando chegar ao banco.
        /// </summary>
        public static Guid GetTenantId(DbContext? context) {
            if (context is not ITenantAwareDbContext tenantAwareContext) {
                throw new InvalidOperationException(
                    "O contexto não informa o tenant da requisição: a operação foi bloqueada antes de chegar ao banco.");
            }

            var tenantContext = tenantAwareContext.TenantContext;

            if (!tenantContext.IsResolved) {
                throw new InvalidOperationException(
                    "O tenant da requisição ainda não foi resolvido: a operação foi bloqueada antes de chegar ao banco.");
            }

            return tenantContext.TenantId;
        }
    }
}
