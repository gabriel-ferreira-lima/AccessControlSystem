using AccessControlSystem.Application.Context.AccountContext.UseCases.Create;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

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

    #region Tamanho máximo (FR-001 a FR-005)

    // O Email.Pattern não limita o tamanho da parte local, então 246 + "@test.com" (9) = 255 só é limitado pelo tamanho.
    private static string EmailComTamanho(int tamanho) => new string('a', tamanho - "@test.com".Length) + "@test.com";

    // Atende ao Password.Pattern (maiúscula, minúscula, dígito e símbolo) com exatamente o tamanho pedido.
    private static string SenhaForteComTamanho(int tamanho) => "Aa1!" + new string('x', tamanho - 4);

    [Fact]
    public void Email_de_exatamente_255_caracteres_com_formato_valido_passa()
    {
        var email = EmailComTamanho(255);

        Assert.Equal(255, email.Length);
        Assert.Matches(Email.Pattern, email);   // o caso "no limite" nunca pode falhar por formato
        Assert.True(Specification.Ensure(Valid() with { Email = email }).IsValid);
    }

    [Fact]
    public void Email_com_256_caracteres_falha_so_pelo_tamanho()
    {
        var contract = Specification.Ensure(Valid() with { Email = EmailComTamanho(256) });

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Email", notification.Key);
        Assert.Equal("E-mail deve ter no máximo 255 caracteres", notification.Message);
    }

    [Fact]
    public void Senha_de_exatamente_128_caracteres_que_atende_ao_formato_passa()
    {
        var senha = SenhaForteComTamanho(128);

        Assert.Equal(128, senha.Length);
        Assert.Matches(Password.Pattern, senha);   // o caso "no limite" nunca pode falhar por formato
        Assert.True(Specification.Ensure(Valid() with { Password = senha }).IsValid);
    }

    [Fact]
    public void Senha_com_129_caracteres_falha_so_pelo_tamanho()
    {
        var senha = SenhaForteComTamanho(129);

        Assert.Matches(Password.Pattern, senha);
        var contract = Specification.Ensure(Valid() with { Password = senha });

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Password", notification.Key);
        Assert.Equal("Senha deve ter no máximo 128 caracteres", notification.Message);
    }

    [Fact]
    public void Email_e_senha_acima_do_limite_geram_uma_notificacao_para_cada_campo()
    {
        var contract = Specification.Ensure(Valid() with { Email = EmailComTamanho(256), Password = SenhaForteComTamanho(129) });

        Assert.Equal(["Email", "Password"], contract.Notifications.Select(n => n.Key).Order().ToArray());
    }

    [Fact]
    public void Email_acima_do_limite_e_de_formato_invalido_recebe_so_a_notificacao_de_tamanho()
    {
        var contract = Specification.Ensure(Valid() with { Email = new string('!', 300) });

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("E-mail deve ter no máximo 255 caracteres", notification.Message);   // o formato nem é avaliado
    }

    [Fact]
    public void Requisicao_comum_continua_valida_depois_do_limite()
    {
        Assert.True(Specification.Ensure(Valid()).IsValid);
    }

    #endregion
}
