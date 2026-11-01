using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Fixtures;

/// <summary>
/// Consultas cruas de verificação usadas pelos testes de migration, de catálogo e de isolamento.
/// Usam a connection string do migrator (dono, BYPASSRLS), então enxergam todas as linhas.
/// </summary>
public static class DatabaseProbe
{
    public static async Task<long> CountAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync() ?? 0L);
    }

    /// <summary>Ids de migration gravados em <c>__EFMigrationsHistory</c>, em ordem. Vazio se a tabela não existe.</summary>
    public static async Task<List<string>> ReadHistoryAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        await using (var exists = new NpgsqlCommand("select to_regclass('\"__EFMigrationsHistory\"') is not null", connection))
        {
            if (!(bool)(await exists.ExecuteScalarAsync() ?? false)) return new List<string>();
        }

        var ids = new List<string>();
        await using var command = new NpgsqlCommand(
            "select \"MigrationId\" from \"__EFMigrationsHistory\" order by \"MigrationId\"", connection);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) ids.Add(reader.GetString(0));
        return ids;
    }

    public static async Task<List<string>> ReadStringsAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync();

        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values;
    }
}
