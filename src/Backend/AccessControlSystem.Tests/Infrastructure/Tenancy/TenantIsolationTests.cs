using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Enums;
using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using CreateRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Create.Repository;
using DeactivateRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Deactivate.Repository;
using GetMeRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.GetMe.Repository;
using GetRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Get.Repository;
using UpdateRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Update.Repository;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// Isolamento entre tenants que DIVIDEM a mesma base (Princípio I): cada teste cria os seus dois
// tenants (A e B), então não depende de dados de outros testes.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class TenantIsolationTests
{
    private readonly PostgresContainerFixture _fixture;

    public TenantIsolationTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Lista_so_devolve_operadores_do_proprio_tenant()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var deA = new[] { await _fixture.SeedOperatorAsync(tenantA), await _fixture.SeedOperatorAsync(tenantA) };
        var deB = new[]
        {
            await _fixture.SeedOperatorAsync(tenantB), await _fixture.SeedOperatorAsync(tenantB),
            await _fixture.SeedOperatorAsync(tenantB)
        };

        await using var context = _fixture.CreateContext(tenantA);
        var result = await new GetRepository(context).GetOperatorList(1, 100, EActiveFilter.All, CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Equal(deA.Select(o => o.Id).Order(), result.Items.Select(i => i.Id).Order());
        Assert.Empty(result.Items.Select(i => i.Id).Intersect(deB.Select(o => o.Id)));
    }

    [Fact]
    public async Task Consulta_por_id_de_outro_tenant_devolve_null()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var deB = await _fixture.SeedOperatorAsync(tenantB);

        await using var context = _fixture.CreateContext(tenantA);

        // Mesma resposta de um id que não existe: nunca confirma que o registro existe em outro tenant.
        Assert.Null(await new GetMeRepository(context).GetUserById(deB.Id));
    }

    [Fact]
    public async Task Mesmo_email_em_dois_tenants_e_aceito()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var email = TenantSeed.UniqueEmail("compartilhado");

        var deA = await _fixture.SeedOperatorAsync(tenantA, email);
        var deB = await _fixture.SeedOperatorAsync(tenantB, email);   // vermelho enquanto o e-mail for único no sistema todo

        Assert.NotEqual(deA.Id, deB.Id);
        await using var contextA = _fixture.CreateContext(tenantA);
        await using var contextB = _fixture.CreateContext(tenantB);
        Assert.Equal(deA.Id, (await contextA.Operators.AsNoTracking().SingleAsync(x => x.Email.Address == email)).Id);
        Assert.Equal(deB.Id, (await contextB.Operators.AsNoTracking().SingleAsync(x => x.Email.Address == email)).Id);
    }

    [Fact]
    public async Task Mesmo_email_no_mesmo_tenant_e_recusado()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var email = TenantSeed.UniqueEmail("duplicado");
        await _fixture.SeedOperatorAsync(tenant, email);

        await using var context = _fixture.CreateContext(tenant);

        await Assert.ThrowsAsync<ConflictException>(
            () => new CreateRepository(context).SaveAsync(TenantSeed.NewOperator(email)));
    }

    [Fact]
    public async Task Alteracao_de_outro_tenant_nao_encontra_o_registro()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var deB = await _fixture.SeedOperatorAsync(tenantB);
        var emailOriginal = deB.Email.Address;

        await using (var context = _fixture.CreateContext(tenantA))
        {
            var repository = new UpdateRepository(context);

            // Não acha para carregar...
            Assert.Null(await repository.GetUserById(deB.Id));

            // ...e mesmo com a entidade em mãos o UPDATE não alcança a linha de outro tenant.
            deB.UpdateEmail(new Email(TenantSeed.UniqueEmail("invasor")));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repository.UpdateAsync(deB));
        }

        // O registro do tenant B continua como estava.
        await using var verify = _fixture.CreateContext(tenantB);
        Assert.Equal(emailOriginal, (await verify.Operators.AsNoTracking().SingleAsync(x => x.Id == deB.Id)).Email.Address);
    }

    [Fact]
    public async Task Contagem_de_admins_considera_so_o_tenant_corrente()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var adminDeA = await _fixture.SeedOperatorAsync(tenantA, role: ERole.Admin);
        for (var i = 0; i < 3; i++) await _fixture.SeedOperatorAsync(tenantB, role: ERole.Admin);

        await using var context = _fixture.CreateContext(tenantA);

        // O tenant A só tem um admin: ele é o último, mesmo que o tenant B tenha três.
        Assert.True(await new DeactivateRepository(context).IsLastActiveAdminAsync(adminDeA.Id));
    }
}
