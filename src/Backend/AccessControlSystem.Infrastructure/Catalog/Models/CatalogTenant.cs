namespace AccessControlSystem.Infrastructure.Catalog.Models {

    /// <summary>
    /// Modelo de persistência de <c>tenants</c>. O catálogo pertence ao sistema de provisionamento:
    /// o CNPJ é texto lido, sem validação aqui (só a unicidade é imposta pelo banco).
    /// </summary>
    public class CatalogTenant {
        public Guid Id { get; set; }
        public string Cnpj { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public Guid DatabaseId { get; set; }
        public CatalogDatabase Database { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }
}
