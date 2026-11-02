using AccessControlSystem.Application.SharedContext.UseCases.Validation;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Tests.Application.SharedContext.Validation;

// FR-001, FR-005: o limite conta caracteres Unicode (não bytes nem unidades UTF-16), é inclusivo e não ecoa o valor.
public class ContractExtensionsTests
{
    private static Contract<Notification> NewContract() => new Contract<Notification>().Requires();

    [Fact]
    public void Texto_nulo_esta_dentro_do_limite()
    {
        var contract = NewContract().IsMaxLength(null, 10, "Campo", "Campo deve ter no máximo 10 caracteres");

        Assert.True(contract.IsValid);
    }

    [Fact]
    public void Texto_com_exatamente_o_maximo_passa()
    {
        var contract = NewContract().IsMaxLength(new string('a', 10), 10, "Campo", "Campo deve ter no máximo 10 caracteres");

        Assert.True(contract.IsValid);
    }

    [Fact]
    public void Texto_com_um_caractere_a_mais_falha_com_a_notificacao_do_campo()
    {
        var contract = NewContract().IsMaxLength(new string('a', 11), 10, "Campo", "Campo deve ter no máximo 10 caracteres");

        Assert.False(contract.IsValid);
        var notification = Assert.Single(contract.Notifications);
        Assert.Equal("Campo", notification.Key);
        Assert.Equal("Campo deve ter no máximo 10 caracteres", notification.Message);
    }

    [Fact]
    public void Conta_caracteres_e_nao_bytes_acentuados_contam_como_um()
    {
        // "é" ocupa 2 bytes em UTF-8, mas é 1 caractere.
        Assert.True(NewContract().IsMaxLength(new string('é', 255), 255, "Email", "m").IsValid);
        Assert.False(NewContract().IsMaxLength(new string('é', 256), 255, "Email", "m").IsValid);
    }

    [Fact]
    public void Emoji_conta_como_um_caractere_embora_ocupe_duas_unidades_utf16()
    {
        // "😀" tem 2 unidades UTF-16 (par substituto), mas é 1 caractere.
        var noLimite = string.Concat(Enumerable.Repeat("😀", 128));
        var acimaDoLimite = string.Concat(Enumerable.Repeat("😀", 129));

        Assert.Equal(256, noLimite.Length);
        Assert.True(NewContract().IsMaxLength(noLimite, 128, "Password", "m").IsValid);
        Assert.False(NewContract().IsMaxLength(acimaDoLimite, 128, "Password", "m").IsValid);
    }

    [Fact]
    public void Texto_muito_acima_do_limite_falha()
    {
        // 1 milhão de caracteres: a recusa não depende de varrer o texto inteiro (curto-circuito), só o resultado é conferido.
        var contract = NewContract().IsMaxLength(new string('a', 1_000_000), 128, "Password", "m");

        Assert.False(contract.IsValid);
    }

    [Fact]
    public void A_mensagem_nao_contem_o_valor_recebido()
    {
        var valor = "SEGREDO-" + new string('x', 20);

        var contract = NewContract().IsMaxLength(valor, 10, "Password", "Senha deve ter no máximo 10 caracteres");

        var notification = Assert.Single(contract.Notifications);
        Assert.DoesNotContain("SEGREDO", notification.Message);
        Assert.DoesNotContain("SEGREDO", notification.Key);
    }

    [Fact]
    public void Devolve_o_mesmo_contrato_e_acumula_campos_diferentes()
    {
        var contract = NewContract();

        var devolvido = contract
            .IsMaxLength(new string('a', 11), 10, "Email", "e")
            .IsMaxLength(new string('b', 11), 10, "Password", "p");

        Assert.Same(contract, devolvido);
        Assert.Equal(["Email", "Password"], contract.Notifications.Select(n => n.Key).ToArray());
    }
}
