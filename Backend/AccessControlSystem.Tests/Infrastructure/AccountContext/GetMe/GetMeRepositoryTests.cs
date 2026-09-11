using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.GetMe;
using AccessControlSystem.Tests.Infrastructure.Fixtures;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.GetMe;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class GetMeRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetMeRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetUserById_devolve_o_operador_com_dados_completos()
    {
        var email = $"getme-{Guid.NewGuid():N}@test.com";
        Operator seeded;
        await using (var context = _fixture.CreateContext())
        {
            seeded = new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Admin);
            context.Operators.Add(seeded);
            await context.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateContext();
        var found = await new Repository(verify).GetUserById(seeded.Id);

        Assert.NotNull(found);
        Assert.Equal(email, found!.Email.Address);
        Assert.Equal(ERole.Admin, found.Role);
    }

    [Fact]
    public async Task GetUserById_null_quando_nao_existe()
    {
        await using var context = _fixture.CreateContext();

        Assert.Null(await new Repository(context).GetUserById(Guid.NewGuid()));
    }
}
