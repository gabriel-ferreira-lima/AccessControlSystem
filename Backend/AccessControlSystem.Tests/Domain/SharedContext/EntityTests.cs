using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

namespace AccessControlSystem.Tests.Domain.SharedContext;

// Cobre o comportamento ATUAL de Entity (IEquatable<Entity> por Id).
// Igualdade estrutural completa (== / != / Equals(object)) ainda não existe — itens 11/12 do painel.
public class EntityTests
{
    private static Operator NewOperator() =>
        new(new Email("e@test.com"), new Password("Str0ng!Pass"), ERole.Operator);

    [Fact]
    public void Equals_true_para_a_mesma_instancia()
    {
        var op = NewOperator();

        Assert.True(op.Equals(op));
    }

    [Fact]
    public void Equals_false_para_null()
    {
        Assert.False(NewOperator().Equals(null));
    }

    [Fact]
    public void Equals_false_para_entidades_com_Ids_diferentes()
    {
        Assert.False(NewOperator().Equals(NewOperator()));
    }

    [Fact]
    public void GetHashCode_deriva_do_Id()
    {
        var op = NewOperator();

        Assert.Equal(op.Id.GetHashCode(), op.GetHashCode());
    }
}
