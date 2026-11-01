using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using GetMeRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.GetMe.Repository;
using GetRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Get.Repository;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// O tenant em base DEDICADA se comporta exatamente como o da base compartilhada: o código não sabe
// onde o tenant mora (Princípio I).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class DedicatedTenantIsolationTests
{
    private readonly PostgresContainerFixture _fixture;

    public DedicatedTenantIsolationTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Tenant_dedicado_enxerga_so_os_proprios_dados()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var seeded = new[]
        {
            await _fixture.SeedOperatorAsync(tenant, dedicated: true),
            await _fixture.SeedOperatorAsync(tenant, dedicated: true)
        };

        await using var context = _fixture.CreateDedicatedContext(tenant);
        var result = await new GetRepository(context).GetOperatorList(1, 100, EActiveFilter.All, CancellationToken.None);

        Assert.Equal(2, result.Total);
        Assert.Equal(seeded.Select(o => o.Id).Order(), result.Items.Select(i => i.Id).Order());
    }

    [Fact]
    public async Task Tenant_dedicado_se_comporta_como_o_compartilhado()
    {
        // A mesma sequência de operações, num tenant compartilhado e num dedicado, dá o mesmo resultado.
        async Task<string> RunAsync(Func<Guid, AccessControlSystem.Infrastructure.Data.AppDbContext> createContext, bool dedicated)
        {
            var tenant = PostgresContainerFixture.NewTenantId();
            var admin = await _fixture.SeedOperatorAsync(tenant, role: ERole.Admin, dedicated: dedicated);
            var comum = await _fixture.SeedOperatorAsync(tenant, role: ERole.Operator, isActive: false, dedicated: dedicated);

            await using var context = createContext(tenant);
            var lista = await new GetRepository(context).GetOperatorList(1, 100, EActiveFilter.All, CancellationToken.None);
            var ativos = await new GetRepository(context).GetOperatorList(1, 100, EActiveFilter.Active, CancellationToken.None);
            var porId = await new GetMeRepository(context).GetUserById(comum.Id);
            var inexistente = await new GetMeRepository(context).GetUserById(Guid.NewGuid());

            return $"total={lista.Total};ativos={ativos.Total};achouPorId={porId is not null};" +
                   $"inativo={porId is { IsActive: false }};inexistente={inexistente is null};admin={admin.Role}";
        }

        var compartilhado = await RunAsync(_fixture.CreateContext, dedicated: false);
        var dedicado = await RunAsync(_fixture.CreateDedicatedContext, dedicated: true);

        Assert.Equal(compartilhado, dedicado);
    }

    [Fact]
    public async Task Tenants_da_base_compartilhada_nao_enxergam_a_base_dedicada()
    {
        var tenantDedicado = PostgresContainerFixture.NewTenantId();
        var deD = await _fixture.SeedOperatorAsync(tenantDedicado, dedicated: true);

        // Nem um tenant qualquer, nem o próprio id do tenant dedicado, enxergam a base dedicada
        // a partir da base compartilhada: os dados estão fisicamente em outro banco.
        await using var outro = _fixture.CreateContext(PostgresContainerFixture.NewTenantId());
        await using var mesmoId = _fixture.CreateContext(tenantDedicado);

        Assert.Null(await new GetMeRepository(outro).GetUserById(deD.Id));
        Assert.Null(await new GetMeRepository(mesmoId).GetUserById(deD.Id));
    }

    [Fact]
    public async Task Mesmo_email_em_tres_tenants_e_aceito()
    {
        var email = TenantSeed.UniqueEmail("tres");

        // SC-005: dois tenants na base compartilhada e um na dedicada, todos com o mesmo e-mail.
        var a = await _fixture.SeedOperatorAsync(PostgresContainerFixture.NewTenantId(), email);
        var b = await _fixture.SeedOperatorAsync(PostgresContainerFixture.NewTenantId(), email);
        var d = await _fixture.SeedOperatorAsync(PostgresContainerFixture.NewTenantId(), email, dedicated: true);

        Assert.Equal(3, new[] { a.Id, b.Id, d.Id }.Distinct().Count());
    }
}
