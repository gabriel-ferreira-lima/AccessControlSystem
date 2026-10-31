using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Create;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Create;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class CreateRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public CreateRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static string UniqueEmail() => $"create-{Guid.NewGuid():N}@test.com";

    private static Operator NewOperator(string email) =>
        new(new Email(email), new Password("Str0ng!Pass"), ERole.Operator);

    [Fact]
    public async Task AnyAsync_false_quando_o_email_nao_existe()
    {
        await using var context = _fixture.CreateContext();
        var repository = new Repository(context);

        Assert.False(await repository.AnyAsync(UniqueEmail()));
    }

    [Fact]
    public async Task AnyAsync_true_depois_de_salvar()
    {
        var email = UniqueEmail();
        await using var context = _fixture.CreateContext();
        var repository = new Repository(context);

        await repository.SaveAsync(NewOperator(email));

        Assert.True(await repository.AnyAsync(email));
    }

    [Fact]
    public async Task SaveAsync_persiste_os_value_objects()
    {
        var email = UniqueEmail();

        await using (var context = _fixture.CreateContext())
            await new Repository(context).SaveAsync(NewOperator(email));

        await using var verify = _fixture.CreateContext();
        var saved = await verify.Operators.AsNoTracking().SingleAsync(o => o.Email.Address == email);

        Assert.Equal(email, saved.Email.Address);
        Assert.Equal(ERole.Operator, saved.Role);
        Assert.True(saved.Password.Verify("Str0ng!Pass"));   // hash reidratado sem passar pelo construtor
    }

    [Fact]
    public async Task SaveAsync_com_email_duplicado_lanca_ConflictException()
    {
        var email = UniqueEmail();

        await using (var context = _fixture.CreateContext())
            await new Repository(context).SaveAsync(NewOperator(email));

        await using var context2 = _fixture.CreateContext();
        var repository = new Repository(context2);

        await Assert.ThrowsAsync<ConflictException>(() => repository.SaveAsync(NewOperator(email)));
    }
}
