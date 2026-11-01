using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

// P-01 (R-14): AutoTransactionBehavior.Always faz até o SaveChanges de um comando abrir transação,
// e o set_config(..., true) feito logo depois do início da transação vale para o INSERT/UPDATE.
[Collection(PremiseCollection.Name)]
[Trait("Category", "Integration")]
public class P01WriteTenantPremiseTests
{
    private readonly PremisePostgresFixture _fixture;

    public P01WriteTenantPremiseTests(PremisePostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Escrita_aplica_o_tenant_e_o_rls_recusa_tenant_diferente()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        // Gravar com o TenantId do próprio tenant corrente funciona.
        await using (var context = _fixture.CreateContext(tenantA))
        {
            var item = new PremiseItem(itemId, "do-tenant-a");
            context.Items.Add(item);
            context.Entry(item).Property<Guid>("TenantId").CurrentValue = tenantA;

            await context.SaveChangesAsync();
        }

        // Conferência pelo migrator (BYPASSRLS): a linha existe e é do tenant A.
        Assert.Equal(tenantA, await _fixture.ReadTenantIdAsync(itemId));

        // Gravar com o TenantId de outro tenant é recusado pelo WITH CHECK da política.
        await using (var context = _fixture.CreateContext(tenantA))
        {
            var intruso = new PremiseItem(Guid.NewGuid(), "do-tenant-b");
            context.Items.Add(intruso);
            context.Entry(intruso).Property<Guid>("TenantId").CurrentValue = tenantB;

            var erro = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

            var postgres = Assert.IsType<PostgresException>(erro.InnerException);
            Assert.Equal("42501", postgres.SqlState);
        }
    }
}
