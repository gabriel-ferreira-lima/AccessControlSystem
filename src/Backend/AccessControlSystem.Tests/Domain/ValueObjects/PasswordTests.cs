using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Exceptions;

namespace AccessControlSystem.Tests.Domain.ValueObjects;

public class PasswordTests
{
    [Theory]
    [InlineData("Str0ng!Pass")]
    [InlineData("aB3$aaaa")]           // 8 chars, um de cada categoria
    [InlineData("Xyz!2025_secure")]
    public void Gera_hash_para_senha_valida(string text)
    {
        var password = new Password(text);

        Assert.False(string.IsNullOrWhiteSpace(password.Hash));
        Assert.DoesNotContain(text, password.Hash);   // nunca guarda o texto puro
    }

    [Theory]
    [InlineData("aB3$aaa")]            // 7 chars
    [InlineData("nospecial1A")]        // sem caractere especial
    [InlineData("NOLOWER1!")]          // sem minúscula
    [InlineData("noupper1!")]          // sem maiúscula
    [InlineData("NoDigits!!")]         // sem dígito
    [InlineData("password")]           // só minúsculas
    public void Rejeita_senha_fraca(string text)
    {
        var ex = Assert.Throws<DomainException>(() => new Password(text));

        Assert.Equal("Senha não atende aos requisitos de segurança", ex.Message);
    }

    [Fact]
    public void Verify_true_para_a_senha_correta()
    {
        var password = new Password("Str0ng!Pass");

        Assert.True(password.Verify("Str0ng!Pass"));
    }

    [Fact]
    public void Verify_false_para_senha_errada()
    {
        var password = new Password("Str0ng!Pass");

        Assert.False(password.Verify("Outra1!Senha"));
    }

    [Fact]
    public void Hashes_da_mesma_senha_sao_diferentes()   // salt aleatório por hash
    {
        var a = new Password("Str0ng!Pass");
        var b = new Password("Str0ng!Pass");

        Assert.NotEqual(a.Hash, b.Hash);
        Assert.True(b.Verify("Str0ng!Pass"));
    }
}
