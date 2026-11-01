using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

/// <summary>
/// Postgres próprio das premissas do R-14 (P-01 a P-06). Usa os papéis reais do ambiente de
/// desenvolvimento: a aplicação (<c>acs_app</c>) não é dona da tabela e o RLS vale para ela.
/// </summary>
public sealed class PremisePostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:18")
        .Build();

    private string _superuserConnectionString = string.Empty;
    private DatabaseConnectionStrings _premises = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _superuserConnectionString = _container.GetConnectionString();

        await PostgresRoleBootstrap.ApplyRolesAsync(_superuserConnectionString);
        _premises = await PostgresRoleBootstrap.CreateDatabaseAsync(
            _superuserConnectionString, "premises", isCatalog: false);

        // A tabela é criada pelo migrator (dono). O acs_app recebe SELECT/INSERT/UPDATE pelos
        // privilégios padrão de tenant-db-grants.sql, e o RLS vale para ele.
        await using var connection = new NpgsqlConnection(_premises.Migrator);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            """
            CREATE TABLE premise_items (
                id uuid PRIMARY KEY,
                name varchar(100) NOT NULL,
                tenant_id uuid NOT NULL
            );
            CREATE INDEX ix_premise_items_tenant_id_id ON premise_items (tenant_id, id);
            ALTER TABLE premise_items ENABLE ROW LEVEL SECURITY;
            ALTER TABLE premise_items FORCE ROW LEVEL SECURITY;
            CREATE POLICY premise_items_tenant_isolation ON premise_items
                USING      (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
            """,
            connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>Contexto da aplicação (<c>acs_app</c>, sujeito ao RLS) para o tenant informado.</summary>
    public PremiseDbContext CreateContext(Guid tenantId, int maxPoolSize = 100) =>
        CreateContext(new FixedTenantContext(tenantId), maxPoolSize);

    public PremiseDbContext CreateContext(ITenantContext tenantContext, int maxPoolSize = 100)
    {
        var builder = new DbContextOptionsBuilder<PremiseDbContext>();
        TenantDbContextOptions.Configure(builder, AppConnectionString(maxPoolSize));
        return new PremiseDbContext(builder.Options, tenantContext);
    }

    /// <summary>Conexão crua como <c>acs_app</c>, sem nenhum tenant informado.</summary>
    public async Task<NpgsqlConnection> OpenAppConnectionAsync(int maxPoolSize = 100)
    {
        var connection = new NpgsqlConnection(AppConnectionString(maxPoolSize));
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>Conexão crua como <c>acs_migrator</c> (dono, com BYPASSRLS): enxerga todos os tenants.</summary>
    public async Task<NpgsqlConnection> OpenMigratorConnectionAsync()
    {
        var connection = new NpgsqlConnection(_premises.Migrator);
        await connection.OpenAsync();
        return connection;
    }

    /// <summary>Banco novo e vazio, com dono migrator, para as premissas de migration (P-06).</summary>
    public Task<DatabaseConnectionStrings> CreateEmptyDatabaseAsync(string name) =>
        PostgresRoleBootstrap.CreateDatabaseAsync(_superuserConnectionString, name, isCatalog: false);

    /// <summary>
    /// Contexto de migrations (sem tenant e sem interceptors), como o Migrator vai usar. Delega ao
    /// <see cref="MigrationContextFactory"/>, que a T069 ajusta quando o construtor do
    /// <c>AppDbContext</c> mudar.
    /// </summary>
    public AppDbContext CreateMigrationContext(string migratorConnectionString) =>
        MigrationContextFactory.Create(migratorConnectionString);

    /// <summary>Insere linhas direto pelo migrator (BYPASSRLS), sem passar pelo EF nem pelos interceptors.</summary>
    public async Task<List<Guid>> SeedAsync(Guid tenantId, int count, string namePrefix)
    {
        var ids = new List<Guid>();
        await using var connection = await OpenMigratorConnectionAsync();
        for (var i = 0; i < count; i++)
        {
            var id = Guid.NewGuid();
            ids.Add(id);
            await using var command = new NpgsqlCommand(
                "INSERT INTO premise_items (id, name, tenant_id) VALUES (@id, @name, @tenant)", connection);
            command.Parameters.AddWithValue("id", id);
            command.Parameters.AddWithValue("name", $"{namePrefix}-{i}");
            command.Parameters.AddWithValue("tenant", tenantId);
            await command.ExecuteNonQueryAsync();
        }
        return ids;
    }

    /// <summary>Lê o <c>tenant_id</c> de uma linha pelo migrator (BYPASSRLS). <c>null</c> se não existe.</summary>
    public async Task<Guid?> ReadTenantIdAsync(Guid itemId)
    {
        await using var connection = await OpenMigratorConnectionAsync();
        await using var command = new NpgsqlCommand("SELECT tenant_id FROM premise_items WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", itemId);
        var value = await command.ExecuteScalarAsync();
        return value is Guid tenant ? tenant : null;
    }

    private string AppConnectionString(int maxPoolSize) =>
        new NpgsqlConnectionStringBuilder(_premises.App) { MaxPoolSize = maxPoolSize, Timeout = 10 }.ConnectionString;
}

[CollectionDefinition(Name)]
public class PremiseCollection : ICollectionFixture<PremisePostgresFixture>
{
    public const string Name = "Premises";
}
