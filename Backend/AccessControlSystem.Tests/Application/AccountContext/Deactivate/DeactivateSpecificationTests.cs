using AccessControlSystem.Application.Context.AccountContext.UseCases.Deactivate;

namespace AccessControlSystem.Tests.Application.AccountContext.Deactivate;

public class DeactivateSpecificationTests
{
    [Fact]
    public void Id_valido_e_diferente_do_solicitante_passa()
    {
        Assert.True(Specification.Ensure(new Request(Guid.NewGuid(), Guid.NewGuid())).IsValid);
    }

    [Fact]
    public void Id_vazio_falha()
    {
        Assert.False(Specification.Ensure(new Request(Guid.Empty, Guid.NewGuid())).IsValid);
    }

    [Fact]
    public void Autodesativacao_falha()
    {
        var id = Guid.NewGuid();

        Assert.False(Specification.Ensure(new Request(id, id)).IsValid);
    }
}
