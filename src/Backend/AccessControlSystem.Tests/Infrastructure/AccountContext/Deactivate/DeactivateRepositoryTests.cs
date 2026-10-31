using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Deactivate;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Deactivate;

// A fixture compartilha o mesmo Postgres entre TODAS as classes de integração (inclusive
// o admin semeado pela migration), então os testes de IsLastActiveAdminAsync medem a
// contagem real de admins ativos ANTES de agir, em vez de assumir que a tabela está vazia.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class DeactivateRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public DeactivateRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static string UniqueEmail() => $"deact-{Guid.NewGuid():N}@test.com";

    private async Task<Operator> SeedOperatorAsync(ERole role, bool isActive = true) {
        var op = new Operator(new Email(UniqueEmail()), new Password("Str0ng!Pass"), role);
        if (!isActive) op.Deactivate();

        await using var context = _fixture.CreateContext();
        context.Operators.Add(op);
        await context.SaveChangesAsync();
        return op;
    }

    private async Task<int> CountActiveAdminsAsync() {
        await using var context = _fixture.CreateContext();
        return await context.Operators.CountAsync(x => x.Role == ERole.Admin && x.IsActive);
    }

    [Fact]
    public async Task GetUserById_devolve_o_operador()
    {
        var seeded = await SeedOperatorAsync(ERole.Operator);
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
    public async Task DeactivateAsync_persiste_o_estado_inativo()
    {
        var op = await SeedOperatorAsync(ERole.Operator);

        await using (var context = _fixture.CreateContext())
        {
            var repository = new Repository(context);
            var tracked = await repository.GetUserById(op.Id);
            tracked!.Deactivate();
            await repository.DeactivateAsync(tracked);
        }

        await using (var verify = _fixture.CreateContext())
        {
            var reloaded = await new Repository(verify).GetUserById(op.Id);
            Assert.False(reloaded!.IsActive);
        }
    }

    [Fact]
    public async Task IsLastActiveAdminAsync_reflete_a_ausencia_de_outros_admins_ativos()
    {
        var activeAdminsBeforeTarget = await CountActiveAdminsAsync();
        var target = await SeedOperatorAsync(ERole.Admin);

        await using var context = _fixture.CreateContext();
        var isLast = await new Repository(context).IsLastActiveAdminAsync(target.Id);

        Assert.Equal(activeAdminsBeforeTarget == 0, isLast);
    }

    [Fact]
    public async Task IsLastActiveAdminAsync_false_quando_ha_outro_admin_ativo_recem_criado()
    {
        await SeedOperatorAsync(ERole.Admin);   // garante +1 admin ativo além do alvo
        var target = await SeedOperatorAsync(ERole.Admin);

        await using var context = _fixture.CreateContext();
        var isLast = await new Repository(context).IsLastActiveAdminAsync(target.Id);

        Assert.False(isLast);
    }

    [Fact]
    public async Task IsLastActiveAdminAsync_admin_inativo_nao_conta_como_outro()
    {
        var activeAdminsBefore = await CountActiveAdminsAsync();
        var target = await SeedOperatorAsync(ERole.Admin);
        await SeedOperatorAsync(ERole.Admin, isActive: false);   // não deveria contar

        await using var context = _fixture.CreateContext();
        var isLast = await new Repository(context).IsLastActiveAdminAsync(target.Id);

        Assert.Equal(activeAdminsBefore == 0, isLast);
    }
}
