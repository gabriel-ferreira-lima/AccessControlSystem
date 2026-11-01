namespace AccessControlSystem.Application.SharedContext.Tenancy {

    /// <summary>Tenant como está no catálogo. O CNPJ é texto lido, sem validação aqui.</summary>
    public sealed record TenantInfo(Guid Id, string Cnpj, string Name, bool IsActive, TenantDatabaseInfo Database);

    /// <summary>Onde o tenant mora. Não contém connection string nem segredo.</summary>
    public sealed record TenantDatabaseInfo(string Name, EDatabaseKind Kind);
}
