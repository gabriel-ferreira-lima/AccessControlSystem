using AccessControlSystem.Application.Context.AccountContext.UseCases.Update;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

namespace AccessControlSystem.Tests.Application.AccountContext.Update;

public class UpdateSpecificationTests
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
        Assert.False(Specification.Ensure(new Request(Guid.Empty, Role: ERole.Admin)).IsValid);
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

    [Fact]
    public void Role_None_falha()
    {
        Assert.False(Specification.Ensure(new Request(Id, Role: ERole.None)).IsValid);
    }

    [Fact]
    public void Role_fora_do_enum_falha()
    {
        Assert.False(Specification.Ensure(new Request(Id, Role: (ERole)99)).IsValid);
    }

    [Fact]
    public void Role_valido_passa()
    {
        Assert.True(Specification.Ensure(new Request(Id, Role: ERole.Admin)).IsValid);
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

        Assert.Matches(Email.Pattern, email);   // o caso "no limite" nunca pode falhar por formato
        Assert.True(Specification.Ensure(new Request(Id, Email: email)).IsValid);
    }

    [Fact]
    public void Email_com_256_caracteres_falha_so_pelo_tamanho()
    {
        var contract = Specification.Ensure(new Request(Id, Email: EmailComTamanho(256)));

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Email", notification.Key);
        Assert.Equal("E-mail deve ter no máximo 255 caracteres", notification.Message);
    }

    [Fact]
    public void Senha_de_exatamente_128_caracteres_que_atende_ao_formato_passa()
    {
        var senha = SenhaForteComTamanho(128);

        Assert.Matches(Password.Pattern, senha);   // o caso "no limite" nunca pode falhar por formato
        Assert.True(Specification.Ensure(new Request(Id, Password: senha)).IsValid);
    }

    [Fact]
    public void Senha_com_129_caracteres_falha_so_pelo_tamanho()
    {
        var contract = Specification.Ensure(new Request(Id, Password: SenhaForteComTamanho(129)));

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Password", notification.Key);
        Assert.Equal("Senha deve ter no máximo 128 caracteres", notification.Message);
    }

    [Fact]
    public void Campos_nao_informados_nao_geram_notificacao_de_tamanho()
    {
        var contract = Specification.Ensure(new Request(Id, Role: ERole.Admin));

        Assert.True(contract.IsValid);
        Assert.Empty(contract.Notifications);
    }

    [Fact]
    public void Email_e_senha_acima_do_limite_geram_uma_notificacao_para_cada_campo()
    {
        var contract = Specification.Ensure(new Request(Id, Email: EmailComTamanho(256), Password: SenhaForteComTamanho(129)));

        Assert.Equal(["Email", "Password"], contract.Notifications.Select(n => n.Key).Order().ToArray());
    }

    [Fact]
    public void Email_acima_do_limite_e_de_formato_invalido_recebe_so_a_notificacao_de_tamanho()
    {
        var contract = Specification.Ensure(new Request(Id, Email: new string('!', 300)));

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("E-mail deve ter no máximo 255 caracteres", notification.Message);   // o formato nem é avaliado
    }

    #endregion
}
