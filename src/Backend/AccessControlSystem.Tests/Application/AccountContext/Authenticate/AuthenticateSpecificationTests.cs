using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

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

    #region Tamanho máximo (FR-001 a FR-005)

    // O Email.Pattern não limita o tamanho da parte local, então 246 + "@test.com" (9) = 255 só é limitado pelo tamanho.
    private static string EmailComTamanho(int tamanho) => new string('a', tamanho - "@test.com".Length) + "@test.com";

    [Fact]
    public void Email_de_exatamente_255_caracteres_com_formato_valido_passa()
    {
        var email = EmailComTamanho(255);

        Assert.Equal(255, email.Length);
        Assert.Matches(Email.Pattern, email);   // o caso "no limite" nunca pode falhar por formato
        Assert.True(Specification.Ensure(new Request(email, "qualquer")).IsValid);
    }

    [Fact]
    public void Email_com_256_caracteres_falha_so_pelo_tamanho()
    {
        var email = EmailComTamanho(256);

        Assert.Matches(Email.Pattern, email);
        var contract = Specification.Ensure(new Request(email, "qualquer"));

        Assert.False(contract.IsValid);
        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Email", notification.Key);
        Assert.Equal("E-mail deve ter no máximo 255 caracteres", notification.Message);
    }

    [Fact]
    public void Senha_de_exatamente_128_caracteres_passa()
    {
        Assert.True(Specification.Ensure(new Request("user@test.com", new string('x', Password.MaxLength))).IsValid);
    }

    [Fact]
    public void Senha_com_129_caracteres_falha_com_a_notificacao_da_senha()
    {
        var contract = Specification.Ensure(new Request("user@test.com", new string('x', Password.MaxLength + 1)));

        Assert.False(contract.IsValid);
        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Password", notification.Key);
        Assert.Equal("Senha deve ter no máximo 128 caracteres", notification.Message);
    }

    [Fact]
    public void Email_e_senha_acima_do_limite_geram_uma_notificacao_para_cada_campo()
    {
        var contract = Specification.Ensure(new Request(EmailComTamanho(256), new string('x', 129)));

        Assert.Equal(["Email", "Password"], contract.Notifications.Select(n => n.Key).Order().ToArray());
    }

    [Fact]
    public void Email_acima_do_limite_e_de_formato_invalido_recebe_so_a_notificacao_de_tamanho()
    {
        var contract = Specification.Ensure(new Request(new string('!', 300), "qualquer"));

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("E-mail deve ter no máximo 255 caracteres", notification.Message);   // o formato nem é avaliado
    }

    [Fact]
    public void Espacos_nas_pontas_contam_para_o_limite_do_email()
    {
        var contract = Specification.Ensure(new Request(EmailComTamanho(255) + " ", "qualquer"));

        Assert.False(contract.IsValid);
        Assert.Contains(contract.Notifications, n => n.Key == "Email");
    }

    [Fact]
    public void Senha_curta_continua_passando_a_specification_porque_o_login_nao_valida_o_formato_da_senha()
    {
        Assert.True(Specification.Ensure(new Request("user@test.com", "abc")).IsValid);
    }

    #endregion
}
