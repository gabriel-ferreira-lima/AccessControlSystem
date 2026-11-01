namespace AccessControlSystem.Tests.Infrastructure.Premises;

/// <summary>Entidade mínima das premissas. O tenant é só a propriedade sombra <c>TenantId</c>.</summary>
public sealed class PremiseItem
{
    private PremiseItem() { }

    public PremiseItem(Guid id, string name)
    {
        Id = id;
        Name = name;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
