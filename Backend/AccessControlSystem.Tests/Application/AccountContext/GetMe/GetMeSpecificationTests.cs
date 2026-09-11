using AccessControlSystem.Application.Context.AccountContext.UseCases.GetMe;

namespace AccessControlSystem.Tests.Application.AccountContext.GetMe;

public class GetMeSpecificationTests
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
