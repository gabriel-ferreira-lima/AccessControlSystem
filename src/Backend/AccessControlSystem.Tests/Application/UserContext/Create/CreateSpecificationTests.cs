using AccessControlSystem.Application.Context.UserContext.Create;

namespace AccessControlSystem.Tests.Application.UserContext.Create;

// O Handler do UserContext/Create é uma classe vazia (legado reescrito pela spec 11) e o use case não está exposto na Api;
// por isso só existem testes de Specification aqui (FR-001 a FR-006).
public class CreateSpecificationTests
{
    private static Request Valid() => new(
        Name: "Maria Silva",
        Telephone: "(11) 91234-5678",
        Password: "1234",
        Rg: "12.345.678-9",
        Cpf: "529.982.247-25",
        Observation: "Visitante frequente");

    [Fact]
    public void Requisicao_comum_continua_valida()
    {
        var contract = Specification.Ensure(Valid());

        Assert.True(contract.IsValid, string.Join("; ", contract.Notifications.Select(n => $"{n.Key}: {n.Message}")));
    }

    [Fact]
    public void Nome_de_exatamente_100_caracteres_passa_e_com_101_falha()
    {
        Assert.True(Specification.Ensure(Valid() with { Name = new string('a', 100) }).IsValid);

        var contract = Specification.Ensure(Valid() with { Name = new string('a', 101) });

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Name", notification.Key);
        Assert.Equal("Campo nome deve ter no máximo 100 caracteres", notification.Message);
    }

    [Fact]
    public void Observacao_de_exatamente_100_caracteres_passa_e_com_101_falha()
    {
        Assert.True(Specification.Ensure(Valid() with { Observation = new string('a', 100) }).IsValid);

        var contract = Specification.Ensure(Valid() with { Observation = new string('a', 101) });

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Observation", notification.Key);
        Assert.Equal("Campo observação deve ter no máximo 100 caracteres", notification.Message);
    }

    [Fact]
    public void Cpf_de_exatamente_14_caracteres_com_formato_valido_passa_e_com_15_falha_so_pelo_tamanho()
    {
        Assert.True(Specification.Ensure(Valid() with { Cpf = "529.982.247-25" }).IsValid);

        var contract = Specification.Ensure(Valid() with { Cpf = "529.982.247-25 " });

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Cpf", notification.Key);
        Assert.Equal("CPF deve ter no máximo 14 caracteres", notification.Message);
    }

    [Fact]
    public void Pin_de_exatamente_6_digitos_passa_e_com_7_falha_so_pelo_tamanho()
    {
        Assert.True(Specification.Ensure(Valid() with { Password = "123456" }).IsValid);

        var contract = Specification.Ensure(Valid() with { Password = "1234567" });

        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Password", notification.Key);
        Assert.Equal("Senha deve ter no máximo 6 caracteres", notification.Message);
    }

    // O padrão de telefone (15 caracteres no máximo) e o de RG (12) são mais curtos que o limite de 20: um texto de
    // exatamente 20 caracteres não tem formato válido, então no limite só se confere que NÃO há notificação de tamanho.
    [Theory]
    [InlineData("Telephone", "Telefone deve ter no máximo 20 caracteres")]
    [InlineData("Rg", "RG deve ter no máximo 20 caracteres")]
    public void Telefone_e_rg_com_20_caracteres_nao_geram_notificacao_de_tamanho_e_com_21_geram_so_ela(string campo, string mensagem)
    {
        Request Com(int tamanho) => campo == "Telephone"
            ? Valid() with { Telephone = new string('9', tamanho) }
            : Valid() with { Rg = new string('9', tamanho) };

        var noLimite = Specification.Ensure(Com(20));
        Assert.DoesNotContain(noLimite.Notifications, n => n.Message == mensagem);

        var acima = Specification.Ensure(Com(21));
        var notification = Assert.Single(acima.Notifications);   // o formato inválido nem é avaliado
        Assert.Equal(campo, notification.Key);
        Assert.Equal(mensagem, notification.Message);
    }

    [Fact]
    public void Campos_acima_do_limite_geram_uma_notificacao_para_cada_campo()
    {
        var contract = Specification.Ensure(Valid() with
        {
            Name = new string('a', 101),
            Observation = new string('a', 101),
            Telephone = new string('9', 21),
            Rg = new string('9', 21),
            Cpf = new string('9', 15),
            Password = "1234567",
        });

        Assert.Equal(
            ["Cpf", "Name", "Observation", "Password", "Rg", "Telephone"],
            contract.Notifications.Select(n => n.Key).Order().ToArray());
    }

    [Fact]
    public void Texto_acima_do_limite_e_de_formato_invalido_recebe_so_a_notificacao_de_tamanho()
    {
        var contract = Specification.Ensure(Valid() with { Cpf = "abcdefghijklmnopq", Password = "abcdefghij" });

        Assert.Equal(["Cpf", "Password"], contract.Notifications.Select(n => n.Key).Order().ToArray());
        Assert.All(contract.Notifications, n => Assert.Contains("no máximo", n.Message));
    }
}
