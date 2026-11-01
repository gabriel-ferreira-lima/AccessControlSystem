using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Create;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

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

    [Fact]
    public async Task SaveAsync_aceita_o_mesmo_email_em_outro_tenant()
    {
        // FR-019: o e-mail é único DENTRO do tenant, não no sistema todo.
        var email = UniqueEmail();
        var outroTenant = PostgresContainerFixture.NewTenantId();
        await _fixture.SeedOperatorAsync(outroTenant, email);

        await using (var context = _fixture.CreateContext())
            await new Repository(context).SaveAsync(NewOperator(email));

        await using var verify = _fixture.CreateContext();
        Assert.True(await verify.Operators.AsNoTracking().AnyAsync(o => o.Email.Address == email));
        await using var verifyOutro = _fixture.CreateContext(outroTenant);
        Assert.True(await verifyOutro.Operators.AsNoTracking().AnyAsync(o => o.Email.Address == email));
    }

    [Fact]
    public async Task SaveAsync_no_mesmo_tenant_lanca_ConflictException()
    {
        var email = UniqueEmail();
        var tenant = PostgresContainerFixture.NewTenantId();
        await _fixture.SeedOperatorAsync(tenant, email);

        await using var context = _fixture.CreateContext(tenant);

        await Assert.ThrowsAsync<ConflictException>(() => new Repository(context).SaveAsync(NewOperator(email)));
    }

    [Fact]
    public async Task Isolamento_nao_enxerga_email_de_outro_tenant()
    {
        var email = UniqueEmail();
        await _fixture.SeedOperatorAsync(PostgresContainerFixture.NewTenantId(), email);

        await using var context = _fixture.CreateContext();   // tenant padrão da fixture

        Assert.False(await new Repository(context).AnyAsync(email));
    }
}
