using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate;

namespace AccessControlSystem.Tests.Application.AccountContext.Authenticate;

public class AuthenticateSpecificationTests
{
    [Fact]
    public void Email_valido_passa()
    {
        Assert.True(Specification.Ensure(new Request("user@test.com", "qualquer")).IsValid);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    [InlineData("a@b")]
    public void Email_invalido_falha(string email)
    {
        Assert.False(Specification.Ensure(new Request(email, "qualquer")).IsValid);
    }
}
