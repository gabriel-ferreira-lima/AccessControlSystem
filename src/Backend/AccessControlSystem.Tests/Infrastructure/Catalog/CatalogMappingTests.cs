using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Catalog;

// Valida o mapeamento do catálogo (CatalogTenantMap/CatalogDatabaseMap) contra um Postgres real.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class CatalogMappingTests
{
    private readonly PostgresContainerFixture _fixture;

    public CatalogMappingTests(PostgresContainerFixture fixture) => _fixture = fixture;

    // O Postgres guarda timestamptz com precisão de microssegundo.
    private static DateTime UtcNow()
    {
        var now = DateTime.UtcNow;
        return new DateTime(now.Ticks - now.Ticks % 10, DateTimeKind.Utc);
    }

    private static string UniqueCnpj() => Guid.NewGuid().ToString("N")[..18];

    private static CatalogDatabase NewDatabase(EDatabaseKind kind = EDatabaseKind.Shared) => new()
    {
        Id = Guid.NewGuid(),
        Name = $"db_{Guid.NewGuid():N}",
        Kind = kind,
        CreatedAt = UtcNow()
    };

    private static CatalogTenant NewTenant(CatalogDatabase database, string? cnpj = null) => new()
    {
        Id = Guid.NewGuid(),
        Cnpj = cnpj ?? UniqueCnpj(),
        Name = "Empresa de Teste",
        IsActive = true,
        DatabaseId = database.Id,
        CreatedAt = UtcNow()
    };

    [Fact]
    public async Task Tenant_faz_round_trip_completo()
    {
        var database = NewDatabase(EDatabaseKind.Dedicated);
        database.SchemaVersion = "20260910225125_AddIsActiveToOperator";
        database.MigrationStatus = EMigrationStatus.Failed;
        database.MigrationAttemptedAt = UtcNow();
        database.MigrationError = "falha de teste";
        var tenant = NewTenant(database);
        tenant.IsActive = false;

        await using (var write = _fixture.CreateCatalogContext(asMigrator: true))
        {
            write.Databases.Add(database);
            write.Tenants.Add(tenant);
            await write.SaveChangesAsync();
        }

        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        var loaded = await read.Tenants.AsNoTracking().Include(x => x.Database).SingleAsync(x => x.Id == tenant.Id);

        Assert.Equal(tenant.Cnpj, loaded.Cnpj);
        Assert.Equal("Empresa de Teste", loaded.Name);
        Assert.False(loaded.IsActive);
        Assert.Equal(tenant.CreatedAt, loaded.CreatedAt);
        Assert.Equal(database.Id, loaded.DatabaseId);
        Assert.Equal(database.Name, loaded.Database.Name);
        Assert.Equal(EDatabaseKind.Dedicated, loaded.Database.Kind);
        Assert.Equal("20260910225125_AddIsActiveToOperator", loaded.Database.SchemaVersion);
        Assert.Equal(EMigrationStatus.Failed, loaded.Database.MigrationStatus);
        Assert.Equal(database.MigrationAttemptedAt, loaded.Database.MigrationAttemptedAt);
        Assert.Equal("falha de teste", loaded.Database.MigrationError);
    }

    [Fact]
    public async Task Cnpj_repetido_e_recusado_pelo_banco()
    {
        var database = NewDatabase();
        var cnpj = UniqueCnpj();

        await using (var write = _fixture.CreateCatalogContext(asMigrator: true))
        {
            write.Databases.Add(database);
            write.Tenants.Add(NewTenant(database, cnpj));
            await write.SaveChangesAsync();
        }

        await using var second = _fixture.CreateCatalogContext(asMigrator: true);
        second.Tenants.Add(NewTenant(database, cnpj));

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());

        Assert.Equal("23505", Assert.IsType<PostgresException>(erro.InnerException).SqlState);
    }

    [Fact]
    public async Task Cnpj_e_gravado_como_texto_sem_validacao()
    {
        var database = NewDatabase();
        var mascarado = new CatalogTenant
        {
            Id = Guid.NewGuid(), Cnpj = "11.222.333/0001-81", Name = "Com máscara",
            DatabaseId = database.Id, CreatedAt = UtcNow()
        };
        var livre = new CatalogTenant
        {
            Id = Guid.NewGuid(), Cnpj = "texto-qualquer", Name = "Sem formato",
            DatabaseId = database.Id, CreatedAt = UtcNow()
        };

        await using (var write = _fixture.CreateCatalogContext(asMigrator: true))
        {
            write.Databases.Add(database);
            write.Tenants.AddRange(mascarado, livre);
            await write.SaveChangesAsync();
        }

        await using var read = _fixture.CreateCatalogContext(asMigrator: false);
        Assert.Equal("11.222.333/0001-81", (await read.Tenants.AsNoTracking().SingleAsync(x => x.Id == mascarado.Id)).Cnpj);
        Assert.Equal("texto-qualquer", (await read.Tenants.AsNoTracking().SingleAsync(x => x.Id == livre.Id)).Cnpj);
    }

    [Fact]
    public async Task Nome_de_base_repetido_e_recusado_pelo_banco()
    {
        var first = NewDatabase();
        var second = NewDatabase();
        second.Name = first.Name;

        await using (var write = _fixture.CreateCatalogContext(asMigrator: true))
        {
            write.Databases.Add(first);
            await write.SaveChangesAsync();
        }

        await using var context = _fixture.CreateCatalogContext(asMigrator: true);
        context.Databases.Add(second);

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal("23505", Assert.IsType<PostgresException>(erro.InnerException).SqlState);
    }

    [Fact]
    public async Task Tenant_exige_uma_base_existente()
    {
        var baseInexistente = NewDatabase();   // nunca é gravada

        await using var context = _fixture.CreateCatalogContext(asMigrator: true);
        context.Tenants.Add(NewTenant(baseInexistente));

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal("23503", Assert.IsType<PostgresException>(erro.InnerException).SqlState);
    }

    [Fact]
    public async Task Xmin_detecta_conflito_de_edicao()
    {
        var database = NewDatabase();
        var tenant = NewTenant(database);
        await using (var write = _fixture.CreateCatalogContext(asMigrator: true))
        {
            write.Databases.Add(database);
            write.Tenants.Add(tenant);
            await write.SaveChangesAsync();
        }

        await using var first = _fixture.CreateCatalogContext(asMigrator: true);
        await using var second = _fixture.CreateCatalogContext(asMigrator: true);
        var carregadoNoPrimeiro = await first.Tenants.SingleAsync(x => x.Id == tenant.Id);
        var carregadoNoSegundo = await second.Tenants.SingleAsync(x => x.Id == tenant.Id);

        carregadoNoPrimeiro.Name = "Primeira edição";
        await first.SaveChangesAsync();

        carregadoNoSegundo.Name = "Segunda edição, sobre dado velho";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task Papel_da_aplicacao_so_le_o_catalogo()
    {
        var database = NewDatabase();
        await using (var write = _fixture.CreateCatalogContext(asMigrator: true))
        {
            write.Databases.Add(database);
            await write.SaveChangesAsync();
        }

        await using var app = _fixture.CreateCatalogContext(asMigrator: false);

        // Ler pode.
        Assert.True(await app.Databases.AsNoTracking().AnyAsync(x => x.Id == database.Id));

        // Escrever não: o acs_app só tem SELECT no catálogo.
        app.Databases.Add(NewDatabase());
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => app.SaveChangesAsync());
        Assert.Equal("42501", Assert.IsType<PostgresException>(erro.InnerException).SqlState);
    }

    [Fact]
    public async Task Catalogo_nao_tem_colunas_de_senha_nem_connection_string()
    {
        var colunas = await DatabaseProbe.ReadStringsAsync(
            _fixture.CatalogMigratorConnectionString,
            "select column_name from information_schema.columns where table_name in ('tenants', 'tenant_databases')");

        Assert.NotEmpty(colunas);

        string[] proibidos = { "password", "senha", "secret", "segredo", "hash", "connection", "token" };
        Assert.All(colunas, coluna =>
            Assert.DoesNotContain(proibidos, p => coluna.Contains(p, StringComparison.OrdinalIgnoreCase)));
    }
}
