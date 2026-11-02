using AccessControlSystem.Infrastructure.Services;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.Services;

// FR-004 a FR-008: um token só vale se o operador existe no tenant, está ativo e a versão de segurança do token é a
// atual. É UMA consulta, executada sob o RLS do tenant (o tenant já foi validado no catálogo pelo pipeline).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class SessionValidatorTests
{
    private readonly PostgresContainerFixture _fixture;

    public SessionValidatorTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private async Task<bool> IsValidAsync(Guid tenantId, Guid operatorId, int securityVersion)
    {
        await using var context = _fixture.CreateContext(tenantId);
        return await new SessionValidator(context).IsValidAsync(operatorId, securityVersion);
    }

    [Fact]
    public async Task Operador_ativo_com_a_versao_atual_e_valido()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenant);

        Assert.True(await IsValidAsync(tenant, op.Id, op.SecurityVersion));
    }

    [Fact]
    public async Task Operador_inativo_nao_e_valido()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenant, isActive: false);

        // Mesmo com a versão certa, a conta desativada não tem sessão válida.
        Assert.False(await IsValidAsync(tenant, op.Id, op.SecurityVersion));
    }

    [Theory]
    [InlineData(1)]    // versão maior que a atual
    [InlineData(-1)]   // versão menor que a atual
    public async Task Versao_diferente_da_atual_nao_e_valida(int diferenca)
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenant);

        Assert.False(await IsValidAsync(tenant, op.Id, op.SecurityVersion + diferenca));
    }

    [Fact]
    public async Task Operador_inexistente_nao_e_valido()
    {
        var tenant = PostgresContainerFixture.NewTenantId();

        Assert.False(await IsValidAsync(tenant, Guid.NewGuid(), 0));
    }

    [Fact]
    public async Task Desativar_e_reativar_invalida_a_versao_anterior()
    {
        // FR-006: reativar não revalida o token emitido antes da desativação (usa os métodos de domínio).
        var tenant = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenant);
        var versaoDoTokenAntigo = op.SecurityVersion;

        await using (var context = _fixture.CreateContext(tenant))
        {
            var salvo = await context.Operators.SingleAsync(x => x.Id == op.Id);
            salvo.Deactivate();
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateContext(tenant))
        {
            var salvo = await context.Operators.SingleAsync(x => x.Id == op.Id);
            salvo.Activate();
            await context.SaveChangesAsync();
        }

        int versaoAtual;
        await using (var context = _fixture.CreateContext(tenant))
            versaoAtual = (await context.Operators.AsNoTracking().SingleAsync(x => x.Id == op.Id)).SecurityVersion;

        Assert.False(await IsValidAsync(tenant, op.Id, versaoDoTokenAntigo));
        Assert.True(await IsValidAsync(tenant, op.Id, versaoAtual));
    }

    // Caso de isolamento do Princípio VII: o mesmo Id, consultado no contexto de OUTRO tenant da mesma base, não é válido.
    [Fact]
    public async Task Operador_de_outro_tenant_nao_e_valido()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var opDoA = await _fixture.SeedOperatorAsync(tenantA);

        Assert.True(await IsValidAsync(tenantA, opDoA.Id, opDoA.SecurityVersion));
        Assert.False(await IsValidAsync(tenantB, opDoA.Id, opDoA.SecurityVersion));
    }

    // O RLS no banco bloqueia mesmo que o filtro do EF seja desligado: o validador não depende só do código.
    [Fact]
    public async Task Rls_bloqueia_a_consulta_mesmo_sem_o_filtro_do_ef()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var opDoA = await _fixture.SeedOperatorAsync(tenantA);

        await using var context = _fixture.CreateContext(tenantB);
        var existe = await context.Operators.IgnoreQueryFilters().AsNoTracking().AnyAsync(x => x.Id == opDoA.Id);

        Assert.False(existe);
    }
}
