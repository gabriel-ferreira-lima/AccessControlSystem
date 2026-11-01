using System.Reflection;
using System.Text.RegularExpressions;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Fixtures;

/// <summary>
/// Cria os papéis e os bancos de teste exatamente como o ambiente de desenvolvimento, a partir dos
/// mesmos SQL de <c>docker/postgres/sql/</c> (embutidos neste assembly). Usado pelas duas fixtures
/// (a das premissas e a <c>PostgresContainerFixture</c>), para o bootstrap não ficar duplicado.
/// </summary>
public static partial class PostgresRoleBootstrap
{
    public const string MigratorRole = "acs_migrator";
    public const string AppRole = "acs_app";

    // Senhas só de teste, de containers efêmeros. Nenhum segredo real.
    public const string MigratorPassword = "acs-migrator-teste";
    public const string AppPassword = "acs-app-teste";

    /// <summary>Cria os papéis (idempotente) e define as senhas de teste.</summary>
    public static async Task ApplyRolesAsync(string superuserConnectionString)
    {
        await using var connection = new NpgsqlConnection(superuserConnectionString);
        await connection.OpenAsync();

        await ExecuteAsync(connection, ReadEmbeddedSql("sql/roles.sql"));
        await ExecuteAsync(connection, $"ALTER ROLE {MigratorRole} PASSWORD '{MigratorPassword}'");
        await ExecuteAsync(connection, $"ALTER ROLE {AppRole} PASSWORD '{AppPassword}'");
    }

    /// <summary>
    /// Cria um banco com dono <c>acs_migrator</c> e aplica os privilégios padrão da aplicação
    /// (<c>catalog-db-grants.sql</c> ou <c>tenant-db-grants.sql</c>). Devolve as connection strings
    /// da aplicação (<c>acs_app</c>, sujeita ao RLS) e do migrator (<c>acs_migrator</c>).
    /// </summary>
    public static async Task<DatabaseConnectionStrings> CreateDatabaseAsync(
        string superuserConnectionString, string databaseName, bool isCatalog, bool temporary = false)
    {
        if (!DatabaseNamePattern().IsMatch(databaseName))
            throw new ArgumentException("Nome de banco de teste inválido.", nameof(databaseName));

        await using (var connection = new NpgsqlConnection(superuserConnectionString))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, $"CREATE DATABASE \"{databaseName}\" OWNER {MigratorRole}");
        }

        // Os privilégios padrão são por banco: precisam rodar conectados ao banco novo. Conexão única,
        // sem pool: um pool por banco criado deixaria conexões ociosas que esgotam o servidor.
        var superuserOnDatabase = ConnectionStringFor(superuserConnectionString, databaseName, pooled: false);
        await using (var connection = new NpgsqlConnection(superuserOnDatabase))
        {
            await connection.OpenAsync();
            await ExecuteAsync(connection, ReadEmbeddedSql(isCatalog ? "sql/catalog-db-grants.sql" : "sql/tenant-db-grants.sql"));
        }

        // Banco temporário (um por teste): sem pool nas bases de tenant e com ociosidade curta nos
        // catálogos (que fazem muitas operações pequenas). O pool padrão do Npgsql guarda conexões
        // ociosas por 5 minutos, por banco, e com dezenas de bancos esgota o max_connections.
        return new DatabaseConnectionStrings(
            databaseName,
            ConnectionStringFor(superuserConnectionString, databaseName, AppRole, AppPassword, temporary, isCatalog),
            ConnectionStringFor(superuserConnectionString, databaseName, MigratorRole, MigratorPassword, temporary, isCatalog));
    }

    private static string ConnectionStringFor(
        string baseConnectionString, string database, string? role = null, string? password = null,
        bool temporary = false, bool isCatalog = false, bool pooled = true)
    {
        var builder = new NpgsqlConnectionStringBuilder(baseConnectionString) { Database = database };
        if (!pooled || (temporary && !isCatalog))
        {
            builder.Pooling = false;
        }
        else if (temporary)
        {
            builder.ConnectionIdleLifetime = 1;
            builder.ConnectionPruningInterval = 1;
        }

        if (role is not null)
        {
            builder.Username = role;
            builder.Password = password;
        }
        return builder.ConnectionString;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static string ReadEmbeddedSql(string logicalName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(logicalName)
            ?? throw new InvalidOperationException($"Recurso embutido '{logicalName}' não encontrado.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex("^[a-z][a-z0-9_]{0,62}$")]
    private static partial Regex DatabaseNamePattern();
}

/// <summary>Connection strings de um banco de teste: a da aplicação (RLS) e a do migrator (DDL).</summary>
public sealed record DatabaseConnectionStrings(string DatabaseName, string App, string Migrator);
