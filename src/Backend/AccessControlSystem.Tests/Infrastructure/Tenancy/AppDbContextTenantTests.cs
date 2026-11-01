using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Premises;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// O AppDbContext conhece o tenant da requisição: preenche o tenant_id nas escritas, filtra por ele
// nas leituras e falha fechado quando o tenant não foi resolvido.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class AppDbContextTenantTests
{
    private readonly PostgresContainerFixture _fixture;

    public AppDbContextTenantTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task SaveChanges_preenche_o_tenant_das_entidades_novas()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var op = TenantSeed.NewOperator();

        await using (var context = _fixture.CreateContext(tenant))
        {
            context.Operators.Add(op);
            await context.SaveChangesAsync();

            Assert.Equal(tenant, context.Entry(op).Property<Guid>("TenantId").CurrentValue);
        }

        Assert.Equal(1L, await DatabaseProbe.CountAsync(
            _fixture.SharedMigratorConnectionString,
            $"select count(*) from operators where id = '{op.Id}' and tenant_id = '{tenant}'"));
    }

    [Fact]
    public async Task SaveChanges_nao_modifica_o_tenant_de_entidade_alterada()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenant);

        await using (var context = _fixture.CreateContext(tenant))
        {
            var tracked = await context.Operators.SingleAsync(x => x.Id == op.Id);
            tracked.Deactivate();
            context.Operators.Update(tracked);   // marca todas as propriedades como modificadas

            await context.SaveChangesAsync();

            Assert.False(context.Entry(tracked).Property<Guid>("TenantId").IsModified);
        }

        Assert.Equal(1L, await DatabaseProbe.CountAsync(
            _fixture.SharedMigratorConnectionString,
            $"select count(*) from operators where id = '{op.Id}' and tenant_id = '{tenant}' and is_active = false"));
    }

    [Fact]
    public async Task SaveChanges_lanca_sem_tenant_resolvido()
    {
        var op = TenantSeed.NewOperator();
        await using var context = _fixture.CreateContext(FixedTenantContext.Unresolved);
        context.Operators.Add(op);

        // Falha fechada: não grava com tenant vazio nem em outro tenant.
        await Assert.ThrowsAsync<InvalidOperationException>(() => context.SaveChangesAsync());

        Assert.Equal(0L, await DatabaseProbe.CountAsync(
            _fixture.SharedMigratorConnectionString, $"select count(*) from operators where id = '{op.Id}'"));
    }

    [Fact]
    public async Task Consulta_lanca_sem_tenant_resolvido()
    {
        await using var context = _fixture.CreateContext(FixedTenantContext.Unresolved);

        await Assert.ThrowsAsync<InvalidOperationException>(() => context.Operators.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Filtro_global_restringe_ao_tenant_corrente()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        await _fixture.SeedOperatorAsync(tenantA);
        await _fixture.SeedOperatorAsync(tenantB);

        // Dois contextos de tenants diferentes no mesmo processo: o filtro é avaliado por contexto
        // (não fica congelado no modelo em cache). O SQL de cada um carrega o tenant dele.
        await using var contextA = _fixture.CreateContext(tenantA);
        await using var contextB = _fixture.CreateContext(tenantB);

        var sqlA = contextA.Operators.ToQueryString();
        var sqlB = contextB.Operators.ToQueryString();

        Assert.Contains(tenantA.ToString(), sqlA);
        Assert.DoesNotContain(tenantB.ToString(), sqlA);
        Assert.Contains(tenantB.ToString(), sqlB);
        Assert.DoesNotContain(tenantA.ToString(), sqlB);
    }

    [Fact]
    public void Operador_request_e_response_nao_expoem_tenant_id()
    {
        const BindingFlags todos = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // Nenhum tipo do Domain e nenhum use case da Application (Request/Response/ResponseData) tem TenantId:
        // o tenant é preenchido pela Infrastructure a partir do tenant resolvido.
        var tipos = typeof(Operator).Assembly.GetTypes()
            .Concat(typeof(AccessControlSystem.Application.SharedContext.UseCases.Response).Assembly.GetTypes()
                .Where(t => t.Namespace is not null
                    && t.Namespace.StartsWith("AccessControlSystem.Application.Context", StringComparison.Ordinal)))
            .ToList();

        Assert.NotEmpty(tipos);
        Assert.All(tipos, tipo =>
            Assert.DoesNotContain(
                tipo.GetProperties(todos),
                p => p.Name.Equals("TenantId", StringComparison.OrdinalIgnoreCase)));
    }
}
