using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AuthRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate.Repository;
using CreateRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Create.Repository;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Authenticate;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class AuthenticateRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public AuthenticateRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static string UniqueEmail() => $"auth-{Guid.NewGuid():N}@test.com";

    [Fact]
    public async Task GetUserByEmailAsync_null_quando_nao_existe()
    {
        await using var context = _fixture.CreateContext();

        Assert.Null(await new AuthRepository(context).GetUserByEmailAsync(UniqueEmail()));
    }

    [Fact]
    public async Task GetUserByEmailAsync_devolve_o_operador_com_a_senha_utilizavel()
    {
        var email = UniqueEmail();

        await using (var seed = _fixture.CreateContext())
            await new CreateRepository(seed).SaveAsync(
                new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Admin));

        await using var context = _fixture.CreateContext();
        var op = await new AuthRepository(context).GetUserByEmailAsync(email);

        Assert.NotNull(op);
        Assert.Equal(email, op!.Email.Address);
        Assert.Equal(ERole.Admin, op.Role);
        Assert.True(op.Password.Verify("Str0ng!Pass"));
    }

    [Fact]
    public async Task Admin_semeado_pela_migration_existe()
    {
        await using var context = _fixture.CreateContext();

        var admin = await new AuthRepository(context).GetUserByEmailAsync("admin@admin.com.br");

        Assert.NotNull(admin);
        Assert.Equal(ERole.Admin, admin!.Role);
    }
}
