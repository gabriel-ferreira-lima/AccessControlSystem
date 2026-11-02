using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.UpdateMe;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.UpdateMe;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class UpdateMeRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public UpdateMeRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static string UniqueEmail() => $"updme-{Guid.NewGuid():N}@test.com";

    private async Task<Operator> SeedOperatorAsync(string email)
    {
        var op = new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Operator);
        await using var context = _fixture.CreateContext();
        context.Operators.Add(op);
        await context.SaveChangesAsync();
        return op;
    }

    [Fact]
    public async Task GetOperatorByIdAsync_devolve_o_operador()
    {
        var seeded = await SeedOperatorAsync(UniqueEmail());
        await using var context = _fixture.CreateContext();

        var found = await new Repository(context).GetOperatorByIdAsync(seeded.Id);

        Assert.NotNull(found);
        Assert.Equal(seeded.Id, found!.Id);
    }

    [Fact]
    public async Task GetOperatorByIdAsync_null_quando_nao_existe()
    {
        await using var context = _fixture.CreateContext();

        Assert.Null(await new Repository(context).GetOperatorByIdAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task EmailExistsAsync_true_quando_outro_operador_tem_o_email()
    {
        var other = await SeedOperatorAsync(UniqueEmail());
        var target = await SeedOperatorAsync(UniqueEmail());
        await using var context = _fixture.CreateContext();

        var emUso = await new Repository(context).EmailExistsAsync(other.Email.Address, target.Id);

        Assert.True(emUso);
    }

    [Fact]
    public async Task EmailExistsAsync_false_para_o_proprio_email()
    {
        var op = await SeedOperatorAsync(UniqueEmail());
        await using var context = _fixture.CreateContext();

        var emUso = await new Repository(context).EmailExistsAsync(op.Email.Address, op.Id);

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
            var tracked = await repository.GetOperatorByIdAsync(op.Id);
            tracked!.UpdateEmail(new Email(novoEmail));
            await repository.UpdateAsync(tracked);
        }

        await using (var verify = _fixture.CreateContext())
        {
            var reloaded = await new Repository(verify).GetOperatorByIdAsync(op.Id);
            Assert.Equal(novoEmail, reloaded!.Email.Address);
        }
    }

    [Fact]
    public async Task UpdateAsync_com_email_colidindo_lanca_ConflictException()
    {
        var other = await SeedOperatorAsync(UniqueEmail());
        var target = await SeedOperatorAsync(UniqueEmail());

        await using var context = _fixture.CreateContext();
        var repository = new Repository(context);
        var tracked = await repository.GetOperatorByIdAsync(target.Id);
        tracked!.UpdateEmail(new Email(other.Email.Address));

        await Assert.ThrowsAsync<ConflictException>(() => repository.UpdateAsync(tracked));
    }

    [Fact]
    public async Task UpdateAsync_aceita_email_que_existe_so_em_outro_tenant()
    {
        // FR-019: trocar para um e-mail que só existe em outro tenant é permitido.
        var deOutro = await _fixture.SeedOperatorAsync(PostgresContainerFixture.NewTenantId());
        var meu = await SeedOperatorAsync(UniqueEmail());

        await using (var context = _fixture.CreateContext())
        {
            var repository = new Repository(context);
            var tracked = await repository.GetOperatorByIdAsync(meu.Id);
            tracked!.UpdateEmail(new Email(deOutro.Email.Address));
            await repository.UpdateAsync(tracked);
        }

        await using var verify = _fixture.CreateContext();
        Assert.Equal(deOutro.Email.Address, (await new Repository(verify).GetOperatorByIdAsync(meu.Id))!.Email.Address);
    }

    [Fact]
    public async Task UpdateAsync_para_email_do_mesmo_tenant_lanca_ConflictException()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var other = await _fixture.SeedOperatorAsync(tenant);
        var target = await _fixture.SeedOperatorAsync(tenant);

        await using var context = _fixture.CreateContext(tenant);
        var repository = new Repository(context);
        var tracked = await repository.GetOperatorByIdAsync(target.Id);
        tracked!.UpdateEmail(new Email(other.Email.Address));

        await Assert.ThrowsAsync<ConflictException>(() => repository.UpdateAsync(tracked));
    }

    [Fact]
    public async Task EmailExistsAsync_considera_so_o_tenant_corrente()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var doTenant = await _fixture.SeedOperatorAsync(tenant);
        var meu = await _fixture.SeedOperatorAsync(tenant);
        var deOutro = await _fixture.SeedOperatorAsync(PostgresContainerFixture.NewTenantId());

        await using var context = _fixture.CreateContext(tenant);
        var repository = new Repository(context);

        Assert.True(await repository.EmailExistsAsync(doTenant.Email.Address, meu.Id));    // em uso no tenant
        Assert.False(await repository.EmailExistsAsync(deOutro.Email.Address, meu.Id));    // só existe em outro tenant
    }

    [Fact]
    public async Task Isolamento_nao_enxerga_operador_nem_email_de_outro_tenant()
    {
        var deOutro = await _fixture.SeedOperatorAsync(PostgresContainerFixture.NewTenantId());
        var meu = await SeedOperatorAsync(UniqueEmail());

        await using var context = _fixture.CreateContext();   // tenant padrão da fixture
        var repository = new Repository(context);

        Assert.Null(await repository.GetOperatorByIdAsync(deOutro.Id));
        Assert.False(await repository.EmailExistsAsync(deOutro.Email.Address, meu.Id));   // o e-mail só existe em outro tenant
    }
}
