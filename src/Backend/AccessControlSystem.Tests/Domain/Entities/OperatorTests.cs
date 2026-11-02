using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

namespace AccessControlSystem.Tests.Domain.Entities;

public class OperatorTests
{
    private static Operator NewOperator(ERole role = ERole.Operator) =>
        new(new Email("op@test.com"), new Password("Str0ng!Pass"), role);

    [Fact]
    public void Construtor_preenche_email_senha_e_perfil()
    {
        var email = new Email("op@test.com");
        var password = new Password("Str0ng!Pass");

        var op = new Operator(email, password, ERole.Admin);

        Assert.Same(email, op.Email);
        Assert.Same(password, op.Password);
        Assert.Equal(ERole.Admin, op.Role);
    }

    [Fact]
    public void Recebe_um_Id_nao_vazio()
    {
        Assert.NotEqual(Guid.Empty, NewOperator().Id);
    }

    [Fact]
    public void Cada_operador_recebe_um_Id_proprio()
    {
        Assert.NotEqual(NewOperator().Id, NewOperator().Id);
    }

    [Fact]
    public void UpdateEmail_troca_o_email()
    {
        var op = NewOperator();
        var novo = new Email("novo@test.com");

        op.UpdateEmail(novo);

        Assert.Same(novo, op.Email);
    }

    [Fact]
    public void UpdatePassword_troca_a_senha()
    {
        var op = NewOperator();
        var nova = new Password("Nova1!Senha");

        op.UpdatePassword(nova);

        Assert.Same(nova, op.Password);
    }

    [Fact]
    public void UpdateRole_troca_o_perfil()
    {
        var op = NewOperator(ERole.Operator);

        op.UpdateRole(ERole.Admin);

        Assert.Equal(ERole.Admin, op.Role);
    }

    #region Versão de segurança (FR-004 a FR-008)

    [Fact]
    public void SecurityVersion_comeca_em_zero()
    {
        Assert.Equal(0, NewOperator().SecurityVersion);
    }

    [Fact]
    public void UpdatePassword_incrementa_a_versao_de_seguranca()
    {
        var op = NewOperator();

        op.UpdatePassword(new Password("Nova1!Senha"));

        Assert.Equal(1, op.SecurityVersion);
    }

    [Fact]
    public void UpdatePassword_com_a_mesma_senha_em_texto_tambem_incrementa()
    {
        // Senha informada conta como troca mesmo igual à atual (decisão do usuário). Um novo Password com o
        // mesmo texto tem sal novo, então o hash também muda.
        var op = NewOperator();

        op.UpdatePassword(new Password("Str0ng!Pass"));

        Assert.Equal(1, op.SecurityVersion);
    }

    [Fact]
    public void UpdateRole_para_perfil_diferente_incrementa_a_versao()
    {
        var op = NewOperator(ERole.Operator);

        op.UpdateRole(ERole.Admin);

        Assert.Equal(1, op.SecurityVersion);
    }

    [Fact]
    public void UpdateRole_para_o_mesmo_perfil_nao_incrementa()
    {
        var op = NewOperator(ERole.Operator);

        op.UpdateRole(ERole.Operator);

        Assert.Equal(0, op.SecurityVersion);
    }

    [Fact]
    public void Deactivate_de_operador_ativo_incrementa_a_versao()
    {
        var op = NewOperator();

        op.Deactivate();

        Assert.False(op.IsActive);
        Assert.Equal(1, op.SecurityVersion);
    }

    [Fact]
    public void Deactivate_de_operador_ja_inativo_nao_incrementa()
    {
        var op = NewOperator();
        op.Deactivate();

        op.Deactivate();

        Assert.Equal(1, op.SecurityVersion);
    }

    [Fact]
    public void Activate_nao_incrementa_a_versao()
    {
        var op = NewOperator();
        op.Deactivate();
        var versaoDepoisDeDesativar = op.SecurityVersion;

        op.Activate();

        Assert.True(op.IsActive);
        Assert.Equal(versaoDepoisDeDesativar, op.SecurityVersion);
    }

    [Fact]
    public void UpdateEmail_nao_incrementa_a_versao()
    {
        // A identidade do operador é o Id: trocar só o e-mail não derruba o token (clarificação de 2026-11-01).
        var op = NewOperator();

        op.UpdateEmail(new Email("novo@test.com"));

        Assert.Equal(0, op.SecurityVersion);
    }

    [Fact]
    public void Desativar_e_reativar_deixa_a_versao_maior_que_a_inicial()
    {
        // FR-006: reativar não revalida token antigo, porque a desativação já mudou a versão.
        var op = NewOperator();
        var versaoInicial = op.SecurityVersion;

        op.Deactivate();
        op.Activate();

        Assert.True(op.SecurityVersion > versaoInicial);
    }

    #endregion
}
