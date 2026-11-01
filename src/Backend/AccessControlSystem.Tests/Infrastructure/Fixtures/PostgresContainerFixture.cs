using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Premises;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AccessControlSystem.Tests.Infrastructure.Fixtures;

/// <summary>
/// Sobe um PostgreSQL real (Docker) uma única vez para toda a coleção de testes de infraestrutura,
/// com os papéis reais do ambiente de desenvolvimento (<c>acs_migrator</c> e <c>acs_app</c>) e três
/// bancos: o catálogo, uma base compartilhada e uma base dedicada. As bases de tenant são migradas
/// pelo mesmo executor do Migrator (que neutraliza a SeedAdmin); os contextos da aplicação usam
/// <c>acs_app</c>, que não é dono das tabelas, então o RLS vale nos testes.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .Build();

    private string _superuserConnectionString = string.Empty;
    private DatabaseConnectionStrings _catalog = null!;
    private DatabaseConnectionStrings _shared = null!;
    private DatabaseConnectionStrings _dedicated = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _superuserConnectionString = _container.GetConnectionString();

        await PostgresRoleBootstrap.ApplyRolesAsync(_superuserConnectionString);

        // Catálogo: migrations aplicadas pelo usuário migrator (DDL), como no ambiente real.
        _catalog = await PostgresRoleBootstrap.CreateDatabaseAsync(
            _superuserConnectionString, "acs_test_catalog", isCatalog: true);
        await using (var catalogContext = CreateCatalogContext(asMigrator: true))
        {
            await catalogContext.Database.MigrateAsync();
        }

        // Bases de tenant: compartilhada e dedicada, migradas pelo executor real (F2).
        _shared = await CreateAndMigrateTenantDatabaseAsync("acs_test_shared");
        _dedicated = await CreateAndMigrateTenantDatabaseAsync("acs_test_dedicated");

        await RegisterCatalogDatabasesAsync();
    }

    /// <summary>
    /// Cadastra no catálogo as duas bases de tenant do teste, com os nomes de referência usados em
    /// <c>TenantDatabases:{name}</c> pela Api (o catálogo guarda o nome, nunca a connection string).
    /// </summary>
    private async Task RegisterCatalogDatabasesAsync()
    {
        await using var context = CreateCatalogContext(asMigrator: true);
        var now = DateTime.UtcNow;
        context.Databases.AddRange(
            new CatalogDatabase { Id = SharedCatalogDatabaseId, Name = SharedCatalogDatabaseName, Kind = EDatabaseKind.Shared, CreatedAt = now },
            new CatalogDatabase { Id = DedicatedCatalogDatabaseId, Name = DedicatedCatalogDatabaseName, Kind = EDatabaseKind.Dedicated, CreatedAt = now });
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private async Task<DatabaseConnectionStrings> CreateAndMigrateTenantDatabaseAsync(string name)
    {
        var database = await PostgresRoleBootstrap.CreateDatabaseAsync(
            _superuserConnectionString, name, isCatalog: false);

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Falha ao migrar a base de teste '{name}': {result.Error}");

        return database;
    }

    #region Tenants nas bases compartilhada e dedicada

    /// <summary>Um <c>Guid</c> novo por chamada: cada teste usa o seu próprio tenant.</summary>
    public static Guid NewTenantId() => Guid.NewGuid();

    /// <summary>Tenant padrão usado por <c>CreateContext()</c> (os testes de repositório que já existiam).</summary>
    public static readonly Guid DefaultTenantId = Guid.Parse("a0a0a0a0-0000-4000-8000-000000000001");

    /// <summary>Connection string do usuário migrator (dono, BYPASSRLS) na base compartilhada: enxerga todos os tenants.</summary>
    public string SharedMigratorConnectionString => _shared.Migrator;

    /// <summary>Connection string do usuário migrator (dono, BYPASSRLS) na base dedicada.</summary>
    public string DedicatedMigratorConnectionString => _dedicated.Migrator;

    /// <summary>Contexto da aplicação (<c>acs_app</c>, sujeito ao RLS) para o tenant padrão da fixture.</summary>
    public AppDbContext CreateContext() => CreateContext(DefaultTenantId);

    /// <summary>Contexto da aplicação na base compartilhada, para o tenant informado.</summary>
    public AppDbContext CreateContext(Guid tenantId) => CreateContext(new FixedTenantContext(tenantId));

    public AppDbContext CreateContext(ITenantContext tenantContext) => CreateContext(_shared.App, tenantContext);

    /// <summary>Contexto da aplicação na base dedicada, para o tenant informado.</summary>
    public AppDbContext CreateDedicatedContext(Guid tenantId) =>
        CreateContext(_dedicated.App, new FixedTenantContext(tenantId));

    /// <summary>Conexão crua como <c>acs_app</c> na base compartilhada, sem nenhum tenant informado.</summary>
    public Task<NpgsqlConnection> OpenAppConnectionAsync() => OpenAsync(_shared.App);

    /// <summary>Conexão crua como <c>acs_app</c> na base dedicada, sem nenhum tenant informado.</summary>
    public Task<NpgsqlConnection> OpenAppConnectionToDedicatedAsync() => OpenAsync(_dedicated.App);

    private static AppDbContext CreateContext(string appConnectionString, ITenantContext tenantContext)
    {
        // A mesma configuração que a Api usa (Npgsql + interceptors de tenant).
        var builder = new DbContextOptionsBuilder<AppDbContext>();
        TenantDbContextOptions.Configure(builder, appConnectionString);

        return new AppDbContext(builder.Options, tenantContext);
    }

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    #endregion

    #region Catálogo e bases novas

    /// <summary>Connection string do usuário migrator (DDL e escrita) no banco do catálogo.</summary>
    public string CatalogMigratorConnectionString => _catalog.Migrator;

    /// <summary>Connection string do papel da aplicação (somente leitura) no catálogo: a que a Api usa.</summary>
    public string CatalogAppConnectionString => _catalog.App;

    /// <summary>Connection string do papel da aplicação (RLS) na base compartilhada.</summary>
    public string SharedAppConnectionString => _shared.App;

    /// <summary>Connection string do papel da aplicação (RLS) na base dedicada.</summary>
    public string DedicatedAppConnectionString => _dedicated.App;

    // Nomes de referência no catálogo (chaves de TenantDatabases:{name}) e ids das duas bases cadastradas.
    public const string SharedCatalogDatabaseName = "shared_01";
    public const string DedicatedCatalogDatabaseName = "dedicated_01";
    public static readonly Guid SharedCatalogDatabaseId = Guid.Parse("d0d0d0d0-0000-4000-8000-000000000001");
    public static readonly Guid DedicatedCatalogDatabaseId = Guid.Parse("d0d0d0d0-0000-4000-8000-000000000002");

    /// <summary>Conexão crua como <c>acs_migrator</c> no banco do catálogo.</summary>
    public Task<NpgsqlConnection> OpenMigratorConnectionAsync() => OpenAsync(_catalog.Migrator);

    /// <summary>
    /// Banco novo e vazio (dono migrator, com os privilégios padrão da aplicação) para testar
    /// migrations. Devolve as connection strings da aplicação (RLS) e do migrator.
    /// </summary>
    public Task<DatabaseConnectionStrings> CreateEmptyDatabaseAsync(string name) =>
        PostgresRoleBootstrap.CreateDatabaseAsync(_superuserConnectionString, name, isCatalog: false, temporary: true);

    /// <summary>Contexto de migrations (sem tenant) para uma connection string de migrator.</summary>
    public AppDbContext CreateMigrationContext(string migratorConnectionString) =>
        MigrationContextFactory.Create(migratorConnectionString);

    /// <summary>Contexto do catálogo como migrator (escrita) ou como aplicação (somente leitura).</summary>
    public CatalogDbContext CreateCatalogContext(bool asMigrator) =>
        CreateCatalogContext(asMigrator ? _catalog.Migrator : _catalog.App);

    /// <summary>Contexto de um banco de catálogo qualquer, pela connection string.</summary>
    public CatalogDbContext CreateCatalogContext(string connectionString) =>
        new(CatalogOptions(connectionString));

    /// <summary>Opções do <see cref="CatalogDbContext"/> para uma connection string (para quem cria contextos por conta própria).</summary>
    public static DbContextOptions<CatalogDbContext> CatalogOptions(string connectionString) =>
        new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString, npg => npg.MigrationsAssembly("AccessControlSystem.Api"))
            .Options;

    /// <summary>
    /// Catálogo novo e isolado (banco próprio, privilégios de catálogo), para testes que contam ou
    /// listam as bases cadastradas: o catálogo da fixture é compartilhado entre todos os testes.
    /// </summary>
    public async Task<DatabaseConnectionStrings> CreateIsolatedCatalogAsync(bool migrate = true)
    {
        var catalog = await PostgresRoleBootstrap.CreateDatabaseAsync(
            _superuserConnectionString, $"cat_{Guid.NewGuid():N}", isCatalog: true, temporary: true);

        if (migrate)
        {
            await using var context = CreateCatalogContext(catalog.Migrator);
            await context.Database.MigrateAsync();
        }

        return catalog;
    }

    #endregion
}

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres";
}
