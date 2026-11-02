using AccessControlSystem.Domain.Contexts.UserContext.Entities;
using AccessControlSystem.Domain.Contexts.UserContext.ValueObjects;

namespace AccessControlSystem.Tests.Domain.Entities;

public class UserTests
{
    [Fact]
    public void Activate_reativa_o_usuario_desativado()
    {
        var user = new User(new Name("Maria Silva"), new Telephone(), new PasswordAccess(), new Rg(), new Cpf());
        user.Deactivate();
        Assert.False(user.IsActive);

        user.Activate();

        Assert.True(user.IsActive);
    }
}
