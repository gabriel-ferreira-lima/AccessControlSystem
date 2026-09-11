using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Exceptions;

namespace AccessControlSystem.Tests.Domain.ValueObjects;

public class EmailTests
{
    [Theory]
    [InlineData("user@test.com")]
    [InlineData("a.b-c+d@sub.domain.co")]
    [InlineData("name'o@test.com")]
    public void Aceita_endereco_valido(string address)
    {
        var email = new Email(address);

        Assert.Equal(address.ToLowerInvariant(), email.Address);
    }

    [Fact]
    public void Normaliza_para_minusculo_e_sem_espacos()
    {
        var email = new Email("  USER@Test.COM  ");

        Assert.Equal("user@test.com", email.Address);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Rejeita_nulo_ou_vazio(string? address)
    {
        var ex = Assert.Throws<DomainException>(() => new Email(address!));

        Assert.Equal("E-mail inválido", ex.Message);
    }

    [Theory]
    [InlineData("a@b")]          // curto demais (< 5)
    [InlineData("abcd")]         // curto demais
    [InlineData("sem-arroba.com")]
    [InlineData("nope@nope")]    // sem TLD
    [InlineData("@test.com")]    // sem parte local
    public void Rejeita_formato_invalido(string address)
    {
        Assert.Throws<DomainException>(() => new Email(address));
    }

    [Fact]
    public void Converte_implicitamente_de_string()
    {
        Email email = "user@test.com";

        Assert.Equal("user@test.com", email.Address);
    }

    [Fact]
    public void Converte_implicitamente_para_string()
    {
        string address = new Email("user@test.com");

        Assert.Equal("user@test.com", address);
    }

    [Fact]
    public void ToString_devolve_o_endereco()
    {
        Assert.Equal("user@test.com", new Email("user@test.com").ToString());
    }
}
