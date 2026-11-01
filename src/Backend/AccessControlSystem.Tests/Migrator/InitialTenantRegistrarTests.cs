using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Migrator;

// O Migrator (usuário migrator) cadastra no catálogo a base e o tenant inicial, a partir da configuração.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class InitialTenantRegistrarTests
{
    private readonly PostgresContainerFixture _fixture;
    private readonly InitialTenantRegistrar _registrar = new();

    public InitialTenantRegistrarTests(PostgresContainerFixture fixture) => _fixture = fixture;

    // Cada teste usa ids, CNPJ e nome de base próprios: a base do catálogo é compartilhada.
    private static InitialTenantOptions NewOptions(
        string? cnpj = null, string? databaseName = null, EDatabaseKind kind = EDatabaseKind.Shared) => new()
    {
        Id = Guid.NewGuid(),
        Cnpj = cnpj ?? Guid.NewGuid().ToString("N")[..18],
        Name = "Empresa Inicial",
        DatabaseName = databaseName ?? $"inicial_{Guid.NewGuid():N}",
        DatabaseKind = kind
    };

    private async Task RegisterAsync(InitialTenantOptions options)
    {
        await using var context = _fixture.CreateCatalogContext(asMigrator: true);
        await _registrar.RegisterAsync(context, options);
    }

    [Fact]
    public async Task Registra_a_base_e_o_tenant_inicial_ativo()
    {
        var options = NewOptions(kind: EDatabaseKind.Dedicated);

        await RegisterAsync(options);

        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        var tenant = await read.Tenants.AsNoTracking().Include(x => x.Database).SingleAsync(x => x.Id == options.Id);
        Assert.True(tenant.IsActive);
        Assert.Equal(options.Cnpj, tenant.Cnpj);
        Assert.Equal("Empresa Inicial", tenant.Name);
        Assert.Equal(options.DatabaseName, tenant.Database.Name);
        Assert.Equal(EDatabaseKind.Dedicated, tenant.Database.Kind);
        Assert.Equal(DateTimeKind.Utc, tenant.CreatedAt.Kind);
        Assert.InRange(tenant.CreatedAt, DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Registro_e_idempotente()
    {
        var options = NewOptions();

        await RegisterAsync(options);
        await RegisterAsync(options);

        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        Assert.Equal(1, await read.Tenants.CountAsync(x => x.Id == options.Id));
        Assert.Equal(1, await read.Databases.CountAsync(x => x.Name == options.DatabaseName));
    }

    [Fact]
    public async Task Recusa_cnpj_ja_usado_por_outro_tenant()
    {
        var first = NewOptions();
        await RegisterAsync(first);
        var second = NewOptions(cnpj: first.Cnpj);

        // A unicidade é imposta só pelo índice do banco.
        await Assert.ThrowsAsync<DbUpdateException>(() => RegisterAsync(second));

        // E nada do segundo registro ficou pela metade (nem o tenant, nem a base dele).
        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        Assert.False(await read.Tenants.AnyAsync(x => x.Id == second.Id));
        Assert.False(await read.Databases.AnyAsync(x => x.Name == second.DatabaseName));
    }

    [Theory]
    [InlineData("00.000.000/0000-00")]   // formatado e inválido
    [InlineData("não-é-cnpj")]
    [InlineData("12345")]
    public async Task Grava_o_cnpj_como_informado_sem_validar(string cnpj)
    {
        // Cada valor só é usado por este teste: o índice único do CNPJ vale no catálogo compartilhado.
        var options = NewOptions(cnpj: cnpj);

        await RegisterAsync(options);

        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        var stored = await read.Tenants.AsNoTracking().SingleAsync(x => x.Id == options.Id);
        Assert.Equal(cnpj, stored.Cnpj);   // sem máscara, sem validação, sem normalização
    }

    [Fact]
    public async Task Reaproveita_a_base_ja_cadastrada()
    {
        var first = NewOptions();
        await RegisterAsync(first);
        var second = NewOptions(databaseName: first.DatabaseName);

        await RegisterAsync(second);

        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        Assert.Equal(1, await read.Databases.CountAsync(x => x.Name == first.DatabaseName));
        var databaseIds = await read.Tenants.Where(x => x.Id == first.Id || x.Id == second.Id)
            .Select(x => x.DatabaseId).Distinct().CountAsync();
        Assert.Equal(1, databaseIds);   // dois tenants na mesma base compartilhada
    }

    [Fact]
    public async Task Recusa_tenant_ja_cadastrado_com_dados_diferentes()
    {
        var options = NewOptions();
        await RegisterAsync(options);
        var divergent = new InitialTenantOptions
        {
            Id = options.Id, Cnpj = Guid.NewGuid().ToString("N")[..18], Name = options.Name,
            DatabaseName = options.DatabaseName, DatabaseKind = options.DatabaseKind
        };

        // O catálogo é do provisionamento: nunca se sobrescreve um tenant com dados diferentes.
        await Assert.ThrowsAsync<InvalidOperationException>(() => RegisterAsync(divergent));

        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        Assert.Equal(options.Cnpj, (await read.Tenants.AsNoTracking().SingleAsync(x => x.Id == options.Id)).Cnpj);
    }

    [Fact]
    public async Task Nao_reativa_tenant_inativo_ja_cadastrado()
    {
        var options = NewOptions();
        await RegisterAsync(options);
        await using (var deactivate = _fixture.CreateCatalogContext(asMigrator: true))
        {
            await deactivate.Tenants.Where(x => x.Id == options.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, false));
        }

        await RegisterAsync(options);

        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        Assert.False((await read.Tenants.AsNoTracking().SingleAsync(x => x.Id == options.Id)).IsActive);
    }

    [Theory]
    [InlineData("id")]
    [InlineData("cnpj")]
    [InlineData("name")]
    [InlineData("database")]
    public async Task Recusa_configuracao_incompleta(string missing)
    {
        var options = NewOptions();
        switch (missing)
        {
            case "id": options.Id = Guid.Empty; break;
            case "cnpj": options.Cnpj = " "; break;
            case "name": options.Name = ""; break;
            case "database": options.DatabaseName = ""; break;
        }

        await Assert.ThrowsAsync<ArgumentException>(() => RegisterAsync(options));
    }
}
