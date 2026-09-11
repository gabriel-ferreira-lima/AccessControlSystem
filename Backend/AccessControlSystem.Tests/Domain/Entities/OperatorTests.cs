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
}
