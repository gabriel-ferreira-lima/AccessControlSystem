using AccessControlSystem.Application.Context.AccountContext.UseCases.Activate;

namespace AccessControlSystem.Tests.Application.AccountContext.Activate;

public class ActivateSpecificationTests
{
    [Fact]
    public void Id_valido_passa()
    {
        Assert.True(Specification.Ensure(new Request(Guid.NewGuid())).IsValid);
    }

    [Fact]
    public void Id_vazio_falha()
    {
        Assert.False(Specification.Ensure(new Request(Guid.Empty)).IsValid);
    }
}
