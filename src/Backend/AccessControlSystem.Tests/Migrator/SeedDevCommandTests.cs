using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using AccessControlSystem.Migrator;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Premises;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AccessControlSystem.Tests.Migrator;

// seed-dev: bases, tenants e operadores sintéticos de desenvolvimento (data-model.md, seção 4). Só roda em
// Development e grava os operadores pelo AppDbContext, sob RLS, como acs_app.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class SeedDevCommandTests
{
    private const string AdminPassword = "Admin!Dev123";
    private const string OperatorPasswordA = "Operador!A123";
    private const string OperatorPasswordB = "Operador!B456";

    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid TenantD = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly PostgresContainerFixture _fixture;

    public SeedDevCommandTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private sealed record Environment(
        DatabaseConnectionStrings Catalog,
        DatabaseConnectionStrings Shared,
        DatabaseConnectionStrings Dedicated,
        Dictionary<string, string?> Settings);

    /// <summary>Catálogo isolado e duas bases de tenant vazias (sem tabelas), como num volume novo do compose.</summary>
    private async Task<Environment> NewEnvironmentAsync(string environmentName = "Development")
    {
        var catalog = await _fixture.CreateIsolatedCatalogAsync();
        var shared = await _fixture.CreateEmptyDatabaseAsync($"seed_shared_{Guid.NewGuid():N}");
        var dedicated = await _fixture.CreateEmptyDatabaseAsync($"seed_dedicated_{Guid.NewGuid():N}");

        var settings = new Dictionary<string, string?>
        {
            ["DOTNET_ENVIRONMENT"] = environmentName,
            ["ConnectionStrings:CatalogMigration"] = catalog.Migrator,
            ["TenantDatabases:shared_01:ConnectionString"] = shared.App,
            ["TenantDatabases:shared_01:MigrationConnectionString"] = shared.Migrator,
            ["TenantDatabases:dedicated_01:ConnectionString"] = dedicated.App,
            ["TenantDatabases:dedicated_01:MigrationConnectionString"] = dedicated.Migrator,
            ["DevSeed:AdminPassword"] = AdminPassword,
            ["DevSeed:OperatorPasswordTenantA"] = OperatorPasswordA,
            ["DevSeed:OperatorPasswordTenantB"] = OperatorPasswordB,
        };
        return new Environment(catalog, shared, dedicated, settings);
    }

    private static Task<int> SeedAsync(Environment environment) =>
        MigratorApplication.RunAsync(
            ["seed-dev"],
            builder => builder.AddInMemoryCollection(environment.Settings),
            output: new StringWriter());

    private static AppDbContext AppContext(string appConnectionString, Guid tenantId)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        TenantDbContextOptions.Configure(builder, appConnectionString);
        return new AppDbContext(builder.Options, new FixedTenantContext(tenantId));
    }

    [Fact]
    public async Task Recusa_fora_de_Development_sem_escrever_nada()
    {
        var environment = await NewEnvironmentAsync("Production");

        var code = await SeedAsync(environment);

        Assert.Equal(ExitCodes.SeedDevRefused, code);
        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        Assert.Empty(await catalog.Tenants.ToListAsync());
        Assert.Empty(await catalog.Databases.ToListAsync());
        // E nenhuma tabela foi criada nas bases de tenant.
        Assert.Equal(0L, await DatabaseProbe.CountAsync(environment.Shared.Migrator,
            "select count(*) from information_schema.tables where table_schema = 'public'"));
        Assert.Equal(0L, await DatabaseProbe.CountAsync(environment.Dedicated.Migrator,
            "select count(*) from information_schema.tables where table_schema = 'public'"));
    }

    [Fact]
    public async Task Recusa_quando_o_ambiente_nao_esta_definido()
    {
        var environment = await NewEnvironmentAsync();
        environment.Settings["DOTNET_ENVIRONMENT"] = null;   // sem ambiente vale Production

        Assert.Equal(ExitCodes.SeedDevRefused, await SeedAsync(environment));
    }

    [Fact]
    public async Task Cria_bases_tenants_e_operadores_sob_rls()
    {
        var environment = await NewEnvironmentAsync();

        var code = await SeedAsync(environment);

        Assert.Equal(ExitCodes.Ok, code);

        // Catálogo: duas bases (compartilhada e dedicada), já atualizadas, e os três tenants ativos.
        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        var databases = await catalog.Databases.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        Assert.Equal(new[] { "dedicated_01", "shared_01" }, databases.Select(x => x.Name));
        Assert.Equal(EDatabaseKind.Dedicated, databases[0].Kind);
        Assert.Equal(EDatabaseKind.Shared, databases[1].Kind);
        Assert.All(databases, d => Assert.Equal(EMigrationStatus.Succeeded, d.MigrationStatus));
        Assert.Equal(3, await catalog.Tenants.CountAsync(x => x.IsActive));

        // Operadores: visíveis pelo AppDbContext de cada tenant (acs_app, sob RLS), e só os dele.
        await using (var a = AppContext(environment.Shared.App, TenantA))
            Assert.Equal(2, await a.Operators.CountAsync());
        await using (var b = AppContext(environment.Shared.App, TenantB))
            Assert.Equal(2, await b.Operators.CountAsync());
        await using (var d = AppContext(environment.Dedicated.App, TenantD))
            Assert.Equal(1, await d.Operators.CountAsync());
        Assert.Equal(4L, await DatabaseProbe.CountAsync(environment.Shared.Migrator, "select count(*) from operators"));
        Assert.Equal(1L, await DatabaseProbe.CountAsync(environment.Dedicated.Migrator, "select count(*) from operators"));
    }

    [Fact]
    public async Task Grava_os_operadores_com_a_connection_string_da_aplicacao_e_nao_com_a_de_migracao()
    {
        // Se o seed usasse a connection string do migrator (BYPASSRLS), uma connection string de
        // aplicação inválida não faria diferença. Com ela inválida, o seed tem de falhar.
        var environment = await NewEnvironmentAsync();
        environment.Settings["TenantDatabases:shared_01:ConnectionString"] =
            environment.Shared.App.Replace(PostgresRoleBootstrap.AppPassword, "senha-errada");

        var code = await SeedAsync(environment);

        Assert.NotEqual(ExitCodes.Ok, code);
        Assert.Equal(0L, await DatabaseProbe.CountAsync(environment.Shared.Migrator, "select count(*) from operators"));
    }

    [Fact]
    public async Task E_idempotente()
    {
        var environment = await NewEnvironmentAsync();

        var first = await SeedAsync(environment);
        var second = await SeedAsync(environment);

        Assert.Equal(ExitCodes.Ok, first);
        Assert.Equal(ExitCodes.Ok, second);
        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        Assert.Equal(3, await catalog.Tenants.CountAsync());
        Assert.Equal(2, await catalog.Databases.CountAsync());
        Assert.Equal(4L, await DatabaseProbe.CountAsync(environment.Shared.Migrator, "select count(*) from operators"));
        Assert.Equal(1L, await DatabaseProbe.CountAsync(environment.Dedicated.Migrator, "select count(*) from operators"));
    }

    [Fact]
    public async Task Mesmo_email_em_tenants_diferentes_com_senhas_diferentes()
    {
        var environment = await NewEnvironmentAsync();
        await SeedAsync(environment);

        async Task<Operator> ReadAsync(string connection, Guid tenant, string email)
        {
            await using var context = AppContext(connection, tenant);
            return await context.Operators.AsNoTracking().SingleAsync(x => x.Email.Address == email);
        }

        // admin@admin.com.br existe nos três tenants (mesma senha de configuração, hashes distintos).
        var adminA = await ReadAsync(environment.Shared.App, TenantA, "admin@admin.com.br");
        var adminB = await ReadAsync(environment.Shared.App, TenantB, "admin@admin.com.br");
        var adminD = await ReadAsync(environment.Dedicated.App, TenantD, "admin@admin.com.br");
        Assert.All(new[] { adminA, adminB, adminD }, a =>
        {
            Assert.Equal(ERole.Admin, a.Role);
            Assert.True(a.Password.Verify(AdminPassword));
        });
        Assert.Equal(3, new[] { adminA.Id, adminB.Id, adminD.Id }.Distinct().Count());

        // operador@exemplo.com existe em A e em B, com senhas DIFERENTES.
        var operatorA = await ReadAsync(environment.Shared.App, TenantA, "operador@exemplo.com");
        var operatorB = await ReadAsync(environment.Shared.App, TenantB, "operador@exemplo.com");
        Assert.True(operatorA.Password.Verify(OperatorPasswordA));
        Assert.False(operatorA.Password.Verify(OperatorPasswordB));
        Assert.True(operatorB.Password.Verify(OperatorPasswordB));
        Assert.False(operatorB.Password.Verify(OperatorPasswordA));
        Assert.Equal(ERole.Operator, operatorA.Role);
    }

    [Fact]
    public async Task Usa_os_ids_e_cnpjs_sinteticos_de_data_model()
    {
        var environment = await NewEnvironmentAsync();
        await SeedAsync(environment);

        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        var tenants = await catalog.Tenants.AsNoTracking().Include(x => x.Database).ToDictionaryAsync(x => x.Id);

        Assert.Equal(("11222333000181", "shared_01"), (tenants[TenantA].Cnpj, tenants[TenantA].Database.Name));
        Assert.Equal(("11444777000161", "shared_01"), (tenants[TenantB].Cnpj, tenants[TenantB].Database.Name));
        Assert.Equal(("11222333000262", "dedicated_01"), (tenants[TenantD].Cnpj, tenants[TenantD].Database.Name));
        Assert.All(tenants.Values, t => Assert.Equal(DateTimeKind.Utc, t.CreatedAt.Kind));
    }

    [Theory]
    [InlineData("DevSeed:AdminPassword", null)]
    [InlineData("DevSeed:AdminPassword", "fraca")]          // não atende aos requisitos de senha
    [InlineData("DevSeed:OperatorPasswordTenantA", null)]
    [InlineData("DevSeed:OperatorPasswordTenantB", "sem-numero-nem-maiuscula")]
    public async Task Sai_com_2_quando_as_senhas_de_dev_faltam_ou_sao_fracas_sem_escrever_nada(string key, string? value)
    {
        var environment = await NewEnvironmentAsync();
        environment.Settings[key] = value;

        var code = await SeedAsync(environment);

        Assert.Equal(ExitCodes.InvalidConfiguration, code);
        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        Assert.Empty(await catalog.Tenants.ToListAsync());
    }
}
