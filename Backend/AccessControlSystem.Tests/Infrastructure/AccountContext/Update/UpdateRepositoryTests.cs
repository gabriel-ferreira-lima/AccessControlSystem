using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Update;
using AccessControlSystem.Tests.Infrastructure.Fixtures;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Update;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class UpdateRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public UpdateRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static string UniqueEmail() => $"upd-{Guid.NewGuid():N}@test.com";

    private async Task<Operator> SeedOperatorAsync(string email, ERole role = ERole.Operator)
    {
        var op = new Operator(new Email(email), new Password("Str0ng!Pass"), role);
        await using var context = _fixture.CreateContext();
        context.Operators.Add(op);
        await context.SaveChangesAsync();
        return op;
    }

    [Fact]
    public async Task GetUserById_devolve_o_operador()
    {
        var seeded = await SeedOperatorAsync(UniqueEmail());
        await using var context = _fixture.CreateContext();

        var found = await new Repository(context).GetUserById(seeded.Id);

        Assert.NotNull(found);
        Assert.Equal(seeded.Id, found!.Id);
    }

    [Fact]
    public async Task GetUserById_null_quando_nao_existe()
    {
        await using var context = _fixture.CreateContext();

        Assert.Null(await new Repository(context).GetUserById(Guid.NewGuid()));
    }

    [Fact]
    public async Task CheckIfEmailIsNotUsed_true_quando_outro_operador_tem_o_email()
    {
        var other = await SeedOperatorAsync(UniqueEmail());
        var target = await SeedOperatorAsync(UniqueEmail());
        await using var context = _fixture.CreateContext();

        var emUso = await new Repository(context).CheckIfEmailIsNotUsed(other.Email.Address, target.Id);

        Assert.True(emUso);
    }

    [Fact]
    public async Task CheckIfEmailIsNotUsed_false_para_o_proprio_email()
    {
        var op = await SeedOperatorAsync(UniqueEmail());
        await using var context = _fixture.CreateContext();

        var emUso = await new Repository(context).CheckIfEmailIsNotUsed(op.Email.Address, op.Id);

        Assert.False(emUso);
    }

    [Fact]
    public async Task UpdateAsync_persiste_as_mudancas()
    {
        var op = await SeedOperatorAsync(UniqueEmail());
        var novoEmail = UniqueEmail();

        await using (var context = _fixture.CreateContext())
        {
            var repository = new Repository(context);
            var tracked = await repository.GetUserById(op.Id);
            tracked!.UpdateEmail(new Email(novoEmail));
            tracked.UpdateRole(ERole.Admin);
            await repository.UpdateAsync(tracked);
        }

        await using (var verify = _fixture.CreateContext())
        {
            var reloaded = await new Repository(verify).GetUserById(op.Id);
            Assert.Equal(novoEmail, reloaded!.Email.Address);
            Assert.Equal(ERole.Admin, reloaded.Role);
        }
    }

    [Fact]
    public async Task UpdateAsync_com_email_colidindo_lanca_ConflictException()
    {
        var other = await SeedOperatorAsync(UniqueEmail());
        var target = await SeedOperatorAsync(UniqueEmail());

        await using var context = _fixture.CreateContext();
        var repository = new Repository(context);
        var tracked = await repository.GetUserById(target.Id);
        tracked!.UpdateEmail(new Email(other.Email.Address));

        await Assert.ThrowsAsync<ConflictException>(() => repository.UpdateAsync(tracked));
    }
}
