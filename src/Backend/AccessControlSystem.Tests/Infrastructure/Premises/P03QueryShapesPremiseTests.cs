using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

// P-03 (R-14): o interceptor de leitura pode avançar o leitor com NextResult e devolver o mesmo
// leitor sem confundir o EF, para os formatos de consulta que os repositórios usam.
[Collection(PremiseCollection.Name)]
[Trait("Category", "Integration")]
public class P03QueryShapesPremiseTests
{
    private readonly PremisePostgresFixture _fixture;

    public P03QueryShapesPremiseTests(PremisePostgresFixture fixture) => _fixture = fixture;

    private sealed record ItemSummary(Guid Id, string Name);

    [Fact]
    public async Task Interceptor_de_leitura_nao_confunde_o_ef()
    {
        var tenant = Guid.NewGuid();
        var ids = await _fixture.SeedAsync(tenant, 5, "item");
        await _fixture.SeedAsync(Guid.NewGuid(), 3, "outro");

        await using var context = _fixture.CreateContext(tenant);

        var primeiro = await context.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Name == "item-1");
        Assert.NotNull(primeiro);

        var unico = await context.Items.AsNoTracking().SingleOrDefaultAsync(x => x.Id == ids[2]);
        Assert.NotNull(unico);
        Assert.Equal(ids[2], unico!.Id);

        Assert.True(await context.Items.AnyAsync(x => x.Name == "item-3"));
        Assert.False(await context.Items.AnyAsync(x => x.Name == "outro-0"));

        Assert.Equal(5, await context.Items.CountAsync());

        var projetados = await context.Items.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ItemSummary(x.Id, x.Name))
            .ToListAsync();
        Assert.Equal(5, projetados.Count);

        var pagina = await context.Items.AsNoTracking()
            .OrderBy(x => x.Name)
            .Skip(1)
            .Take(2)
            .Select(x => x.Name)
            .ToListAsync();
        Assert.Equal(new[] { "item-1", "item-2" }, pagina);

        var rastreados = await context.Items.ToListAsync();
        Assert.Equal(5, rastreados.Count);

        var naoRastreados = await context.Items.AsNoTracking().ToListAsync();
        Assert.Equal(5, naoRastreados.Count);
    }

    [Fact]
    public async Task Comando_que_nao_e_consulta_fora_de_transacao_falha_fechado()
    {
        var tenant = Guid.NewGuid();
        await _fixture.SeedAsync(tenant, 2, "item");
        await using var context = _fixture.CreateContext(tenant);

        // Sem tenant no banco, o RLS afetaria 0 linhas em silêncio: o interceptor lança em vez disso.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Items.ExecuteUpdateAsync(s => s.SetProperty(x => x.Name, "alterado")));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => context.Items.ExecuteDeleteAsync());
    }
}
