using AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe;

namespace AccessControlSystem.Tests.Application.AccountContext.UpdateMe;

public class UpdateMeSpecificationTests
{
    private static readonly Guid Id = Guid.NewGuid();

    [Fact]
    public void Id_valido_com_um_campo_passa()
    {
        Assert.True(Specification.Ensure(new Request(Id, Email: "novo@test.com")).IsValid);
    }

    [Fact]
    public void Id_vazio_falha()
    {
        Assert.False(Specification.Ensure(new Request(Guid.Empty, Email: "novo@test.com")).IsValid);
    }

    [Fact]
    public void Nenhum_campo_informado_falha()
    {
        Assert.False(Specification.Ensure(new Request(Id)).IsValid);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("a@b")]
    public void Email_invalido_falha(string email)
    {
        Assert.False(Specification.Ensure(new Request(Id, Email: email)).IsValid);
    }

    [Theory]
    [InlineData("Ab1!")]          // < 8
    [InlineData("semnumeros!A")]  // sem dígito
    [InlineData("abcdefghij")]    // só minúsculas
    public void Senha_fora_dos_requisitos_falha(string senha)
    {
        Assert.False(Specification.Ensure(new Request(Id, Password: senha)).IsValid);
    }

    [Fact]
    public void Senha_forte_passa()
    {
        Assert.True(Specification.Ensure(new Request(Id, Password: "Str0ng!Pass")).IsValid);
    }
}
