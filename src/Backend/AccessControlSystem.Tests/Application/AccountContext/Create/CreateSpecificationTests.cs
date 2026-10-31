using AccessControlSystem.Application.Context.AccountContext.UseCases.Create;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;

namespace AccessControlSystem.Tests.Application.AccountContext.Create;

public class CreateSpecificationTests
{
    private static Request Valid() => new("user@test.com", "Str0ng!Pass", ERole.Operator);

    [Fact]
    public void Requisicao_valida_passa()
    {
        Assert.True(Specification.Ensure(Valid()).IsValid);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("")]
    [InlineData("a@b")]
    public void Email_invalido_falha(string email)
    {
        Assert.False(Specification.Ensure(Valid() with { Email = email }).IsValid);
    }

    [Theory]
    [InlineData("Ab1!")]           // < 8
    [InlineData("abcdefghij")]     // sem maiúscula / dígito / especial
    [InlineData("SENHA12345!")]    // sem minúscula
    [InlineData("SenhaSemNumero!")] // sem dígito
    [InlineData("Senha12345")]     // sem caractere especial
    public void Senha_fora_dos_requisitos_falha(string senha)
    {
        Assert.False(Specification.Ensure(Valid() with { Password = senha }).IsValid);
    }

    [Fact]
    public void Perfil_None_falha()
    {
        Assert.False(Specification.Ensure(Valid() with { Role = ERole.None }).IsValid);
    }

    [Fact]
    public void Perfil_fora_do_enum_falha()
    {
        Assert.False(Specification.Ensure(Valid() with { Role = (ERole)99 }).IsValid);
    }

    [Fact]
    public void Requisicao_invalida_produz_notificacoes()
    {
        var contract = Specification.Ensure(Valid() with { Email = "nope", Role = ERole.None });

        Assert.NotEmpty(contract.Notifications);
    }
}
