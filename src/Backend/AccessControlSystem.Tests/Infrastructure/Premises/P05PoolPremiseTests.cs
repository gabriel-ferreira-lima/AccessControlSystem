using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

// P-05 (R-14): com um pool de UMA conexão e tenants alternando, nada vaza entre requisições:
// cada tenant só vê o seu e uma conexão sem tenant informado não vê linha alguma.
[Collection(PremiseCollection.Name)]
[Trait("Category", "Integration")]
public class P05PoolPremiseTests
{
    private const int UmaConexao = 1;

    private readonly PremisePostgresFixture _fixture;

    public P05PoolPremiseTests(PremisePostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Pool_de_uma_conexao_alternando_tenants_nao_vaza()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var deA = await _fixture.SeedAsync(tenantA, 2, "a");
        var deB = await _fixture.SeedAsync(tenantB, 3, "b");

        for (var rodada = 0; rodada < 3; rodada++)
        {
            // Leitura do tenant A.
            await using (var context = _fixture.CreateContext(tenantA, UmaConexao))
            {
                var ids = await context.Items.AsNoTracking().Select(x => x.Id).ToListAsync();
                Assert.All(deA, id => Assert.Contains(id, ids));
                Assert.Empty(ids.Intersect(deB));
            }

            // Leitura do tenant B, na mesma conexão física.
            await using (var context = _fixture.CreateContext(tenantB, UmaConexao))
            {
                var ids = await context.Items.AsNoTracking().Select(x => x.Id).ToListAsync();
                Assert.All(deB, id => Assert.Contains(id, ids));
                Assert.Empty(ids.Intersect(deA));
            }

            // Escrita do tenant A, depois do B.
            foreach (var tenant in new[] { tenantA, tenantB })
            {
                await using var context = _fixture.CreateContext(tenant, UmaConexao);
                var item = new PremiseItem(Guid.NewGuid(), $"rodada-{rodada}");
                context.Items.Add(item);
                context.Entry(item).Property<Guid>("TenantId").CurrentValue = tenant;
                await context.SaveChangesAsync();

                Assert.Equal(tenant, await _fixture.ReadTenantIdAsync(item.Id));
            }
        }

        // Conexão sem tenant informado, na mesma conexão física do pool: 0 linhas.
        await using var semTenant = _fixture.CreateContext(tenantA, UmaConexao);
        await semTenant.Database.OpenConnectionAsync();
        await using var command = semTenant.Database.GetDbConnection().CreateCommand();
        command.CommandText = "select count(*) from premise_items";
        var total = (long)(await command.ExecuteScalarAsync() ?? 0L);

        Assert.Equal(0L, total);
    }
}
