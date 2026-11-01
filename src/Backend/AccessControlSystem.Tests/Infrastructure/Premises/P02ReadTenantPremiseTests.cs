using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

// P-02 (R-14), a premissa mais importante: uma consulta SEM transação explícita recebe o tenant
// (set_config no mesmo lote do comando) e o valor NÃO sobrevive ao lote, o que mantém o tenant
// correto atrás de um pooler em modo transação.
[Collection(PremiseCollection.Name)]
[Trait("Category", "Integration")]
public class P02ReadTenantPremiseTests
{
    private readonly PremisePostgresFixture _fixture;

    public P02ReadTenantPremiseTests(PremisePostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Leitura_aplica_o_tenant_apenas_no_proprio_lote()
    {
        var tenant = Guid.NewGuid();
        var outro = Guid.NewGuid();
        var meus = await _fixture.SeedAsync(tenant, 2, "meu");
        var alheios = await _fixture.SeedAsync(outro, 3, "alheio");

        // Pool de uma conexão: a consulta e a verificação seguinte usam a MESMA conexão física.
        await using var context = _fixture.CreateContext(tenant, maxPoolSize: 1);

        // Leitura sem transação explícita.
        var ids = await context.Items.AsNoTracking().Select(x => x.Id).ToListAsync();

        Assert.Equal(meus.OrderBy(x => x), ids.OrderBy(x => x));
        Assert.Empty(ids.Intersect(alheios));

        // O valor do tenant não sobreviveu ao lote: na mesma conexão física ele está vazio.
        await context.Database.OpenConnectionAsync();
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "select current_setting('app.tenant_id', true)";
        var residual = await command.ExecuteScalarAsync();

        Assert.True(
            residual is null || residual is DBNull || (residual is string texto && texto.Length == 0),
            $"O app.tenant_id sobreviveu ao lote: '{residual}'.");
    }

    [Fact]
    public async Task Contexto_sem_tenant_resolvido_lanca_antes_de_executar_a_consulta()
    {
        await _fixture.SeedAsync(Guid.NewGuid(), 1, "qualquer");
        await using var context = _fixture.CreateContext(FixedTenantContext.Unresolved);

        // Falha fechada: não devolve lista vazia nem cai em outro tenant.
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Items.AsNoTracking().ToListAsync());

        Assert.Contains("tenant", erro.Message, StringComparison.OrdinalIgnoreCase);
    }
}
