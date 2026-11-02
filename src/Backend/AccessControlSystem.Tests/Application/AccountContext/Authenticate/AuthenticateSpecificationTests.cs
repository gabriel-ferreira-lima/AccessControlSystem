using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate;

namespace AccessControlSystem.Tests.Application.AccountContext.Authenticate;

public class AuthenticateSpecificationTests
{
    [Fact]
    public void Email_valido_passa()
    {
        Assert.True(Specification.Ensure(new Request("user@test.com", "qualquer")).IsValid);
    }

    // Decisão R-09: espaços nas pontas e maiúsculas são normalizados pelo login, então não podem barrar a validação.
    [Theory]
    [InlineData("  user@test.com ")]
    [InlineData("User@Test.COM")]
    public void Email_com_espacos_nas_pontas_ou_maiusculas_passa(string email)
    {
        Assert.True(Specification.Ensure(new Request(email, "qualquer")).IsValid);
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
