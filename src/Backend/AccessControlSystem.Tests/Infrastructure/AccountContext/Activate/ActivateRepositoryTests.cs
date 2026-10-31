using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Activate;
using AccessControlSystem.Tests.Infrastructure.Fixtures;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Activate;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class ActivateRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public ActivateRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private async Task<Operator> SeedInactiveOperatorAsync() {
        var op = new Operator(new Email($"act-{Guid.NewGuid():N}@test.com"), new Password("Str0ng!Pass"), ERole.Operator);
        op.Deactivate();

        await using var context = _fixture.CreateContext();
        context.Operators.Add(op);
        await context.SaveChangesAsync();
        return op;
    }

    [Fact]
    public async Task GetUserById_devolve_o_operador()
    {
        var seeded = await SeedInactiveOperatorAsync();
        await using var context = _fixture.CreateContext();

        var found = await new Repository(context).GetUserById(seeded.Id);

        Assert.NotNull(found);
        Assert.False(found!.IsActive);
    }

    [Fact]
    public async Task GetUserById_null_quando_nao_existe()
    {
        await using var context = _fixture.CreateContext();

        Assert.Null(await new Repository(context).GetUserById(Guid.NewGuid()));
    }

    [Fact]
    public async Task ActivateAsync_persiste_o_estado_ativo()
    {
        var op = await SeedInactiveOperatorAsync();

        await using (var context = _fixture.CreateContext())
        {
            var repository = new Repository(context);
            var tracked = await repository.GetUserById(op.Id);
            tracked!.Activate();
            await repository.ActivateAsync(tracked);
        }

        await using (var verify = _fixture.CreateContext())
        {
            var reloaded = await new Repository(verify).GetUserById(op.Id);
            Assert.True(reloaded!.IsActive);
        }
    }
}
