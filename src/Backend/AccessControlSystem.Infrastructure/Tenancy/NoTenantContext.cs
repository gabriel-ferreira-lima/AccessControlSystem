using AccessControlSystem.Application.SharedContext.Tenancy;

namespace AccessControlSystem.Infrastructure.Tenancy {

    /// <summary>
    /// "Sem tenant": usado onde não existe tenant por definição, como ao aplicar migrations e nas
    /// ferramentas do EF em tempo de design. Nunca resolvido, então qualquer uso de dado de tenant
    /// por este contexto falha fechado. Sem instância estática compartilhada (Princípio I).
    /// </summary>
    public sealed class NoTenantContext : ITenantContext {

        public bool IsResolved => false;

        public Guid TenantId =>
            throw new InvalidOperationException("Este contexto não tem tenant: ele serve a migrations e a ferramentas de design-time.");
    }
}
