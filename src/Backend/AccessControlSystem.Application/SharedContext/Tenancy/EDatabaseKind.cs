namespace AccessControlSystem.Application.SharedContext.Tenancy {

    /// <summary>Tipo de base de dados de tenant.</summary>
    public enum EDatabaseKind {

        /// <summary>Vários tenants dividem a base, isolados por <c>tenant_id</c> e RLS.</summary>
        Shared = 1,

        /// <summary>Base de um único tenant (por volume ou contrato).</summary>
        Dedicated = 2
    }
}
