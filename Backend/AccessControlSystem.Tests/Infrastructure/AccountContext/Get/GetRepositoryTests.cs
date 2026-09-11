using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Get;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Get;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class GetRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public GetRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private async Task SeedOperatorsAsync(int quantidade)
    {
        await using var context = _fixture.CreateContext();
        for (var i = 0; i < quantidade; i++)
            context.Operators.Add(new Operator(
                new Email($"get-{Guid.NewGuid():N}@test.com"), new Password("Str0ng!Pass"), ERole.Operator));
        await context.SaveChangesAsync();
    }

    private async Task<Operator> SeedActiveOperatorAsync()
    {
        var op = new Operator(new Email($"get-{Guid.NewGuid():N}@test.com"), new Password("Str0ng!Pass"), ERole.Operator);
        await using var context = _fixture.CreateContext();
        context.Operators.Add(op);
        await context.SaveChangesAsync();
        return op;
    }

    private async Task<Operator> SeedInactiveOperatorAsync()
    {
        var op = new Operator(new Email($"get-{Guid.NewGuid():N}@test.com"), new Password("Str0ng!Pass"), ERole.Operator);
        op.Deactivate();
        await using var context = _fixture.CreateContext();
        context.Operators.Add(op);
        await context.SaveChangesAsync();
        return op;
    }

    [Fact]
    public async Task Devolve_a_pagina_com_o_total_do_banco()
    {
        await SeedOperatorsAsync(3);
        await using var context = _fixture.CreateContext();
        var totalReal = await context.Operators.CountAsync();

        var result = await new Repository(context).GetOperatorList(page: 1, size: 2, EActiveFilter.All, CancellationToken.None);

        Assert.Equal(totalReal, result.Total);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.Size);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Itens_vem_ordenados_por_id()
    {
        await SeedOperatorsAsync(5);
        await using var context = _fixture.CreateContext();

        var result = await new Repository(context).GetOperatorList(1, 5, EActiveFilter.All, CancellationToken.None);

        var ids = result.Items.Select(i => i.Id).ToList();
        Assert.Equal(ids.OrderBy(id => id).ToList(), ids);
    }

    [Fact]
    public async Task Paginas_diferentes_nao_repetem_itens()
    {
        await SeedOperatorsAsync(6);
        await using var context = _fixture.CreateContext();
        var repository = new Repository(context);

        var pagina1 = await repository.GetOperatorList(1, 3, EActiveFilter.All, CancellationToken.None);
        var pagina2 = await repository.GetOperatorList(2, 3, EActiveFilter.All, CancellationToken.None);

        var interseccao = pagina1.Items.Select(i => i.Id).Intersect(pagina2.Items.Select(i => i.Id));
        Assert.Empty(interseccao);
    }

    [Fact]
    public async Task Projeta_id_email_e_role()
    {
        var email = $"get-{Guid.NewGuid():N}@test.com";
        await using (var seed = _fixture.CreateContext())
        {
            seed.Operators.Add(new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Admin));
            await seed.SaveChangesAsync();
        }

        await using var context = _fixture.CreateContext();
        var result = await new Repository(context).GetOperatorList(1, 500, EActiveFilter.All, CancellationToken.None);

        var item = Assert.Single(result.Items, i => i.Email == email);
        Assert.NotEqual(Guid.Empty, item.Id);
        Assert.Equal(ERole.Admin, item.Role);
    }

    [Fact]
    public async Task Filtro_Active_nao_traz_operadores_inativos()
    {
        var inactive = await SeedInactiveOperatorAsync();

        await using var context = _fixture.CreateContext();
        var result = await new Repository(context).GetOperatorList(1, 500, EActiveFilter.Active, CancellationToken.None);

        Assert.DoesNotContain(result.Items, i => i.Id == inactive.Id);
    }

    [Fact]
    public async Task Filtro_Inactive_traz_so_operadores_inativos()
    {
        var active = await SeedActiveOperatorAsync();
        var inactive = await SeedInactiveOperatorAsync();

        await using var context = _fixture.CreateContext();
        var result = await new Repository(context).GetOperatorList(1, 500, EActiveFilter.Inactive, CancellationToken.None);

        Assert.Contains(result.Items, i => i.Id == inactive.Id);
        Assert.DoesNotContain(result.Items, i => i.Id == active.Id);
    }

    [Fact]
    public async Task Filtro_All_traz_ativos_e_inativos()
    {
        var active = await SeedActiveOperatorAsync();
        var inactive = await SeedInactiveOperatorAsync();

        await using var context = _fixture.CreateContext();
        var result = await new Repository(context).GetOperatorList(1, 500, EActiveFilter.All, CancellationToken.None);

        Assert.Contains(result.Items, i => i.Id == active.Id);
        Assert.Contains(result.Items, i => i.Id == inactive.Id);
    }
}
