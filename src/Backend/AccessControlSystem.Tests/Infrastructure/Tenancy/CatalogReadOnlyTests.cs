using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// FR-020a / P10: no catálogo o acs_app só LÊ. A escrita é do migrator (e, no futuro, do provisionamento). Se uma spec
// futura der escrita em tabelas específicas do catálogo (ex.: o diretório de operadores, spec 09), ela ajusta este teste.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class CatalogReadOnlyTests
{
    private readonly PostgresContainerFixture _fixture;

    public CatalogReadOnlyTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private async Task<List<string>> CatalogTablesAsync() =>
        await DatabaseProbe.ReadStringsAsync(
            _fixture.CatalogMigratorConnectionString,
            "select tablename from pg_tables where schemaname = 'public' order by tablename");

    private async Task<string> AnyColumnAsync(string table) =>
        (await DatabaseProbe.ReadStringsAsync(
            _fixture.CatalogMigratorConnectionString,
            $"select column_name from information_schema.columns where table_schema = 'public' and table_name = '{table}' order by ordinal_position limit 1")).First();

    [Fact]
    public async Task O_catalogo_tem_as_tabelas_esperadas()
    {
        var tables = await CatalogTablesAsync();

        Assert.Contains("tenants", tables);
        Assert.Contains("tenant_databases", tables);
    }

    [Fact]
    public async Task Acs_app_tem_so_select_em_toda_tabela_do_catalogo()
    {
        foreach (var table in await CatalogTablesAsync())
        {
            Assert.Equal(1L, await DatabaseProbe.CountAsync(_fixture.CatalogMigratorConnectionString,
                $"select case when has_table_privilege('acs_app', 'public.\"{table}\"', 'SELECT') then 1 else 0 end"));

            foreach (var privilege in new[] { "INSERT", "UPDATE", "DELETE", "TRUNCATE" })
            {
                Assert.True(
                    0L == await DatabaseProbe.CountAsync(_fixture.CatalogMigratorConnectionString,
                        $"select case when has_table_privilege('acs_app', 'public.\"{table}\"', '{privilege}') then 1 else 0 end"),
                    $"O acs_app tem {privilege} em {table} no catálogo (deveria ser só leitura).");
            }
        }
    }

    [Fact]
    public async Task Acs_app_le_o_catalogo()
    {
        await using var connection = new NpgsqlConnection(_fixture.CatalogAppConnectionString);
        await connection.OpenAsync();

        foreach (var table in await CatalogTablesAsync())
        {
            await using var command = new NpgsqlCommand($"select count(*) from public.\"{table}\"", connection);
            Assert.True(Convert.ToInt64(await command.ExecuteScalarAsync()) >= 0);
        }
    }

    [Fact]
    public async Task Insert_update_e_delete_no_catalogo_sao_negados_ao_acs_app()
    {
        foreach (var table in await CatalogTablesAsync())
        {
            var column = await AnyColumnAsync(table);

            // A checagem de privilégio vem antes de qualquer restrição ou linha: DEFAULT VALUES e WHERE false bastam.
            var statements = new[]
            {
                $"insert into public.\"{table}\" default values",
                $"update public.\"{table}\" set \"{column}\" = \"{column}\" where false",
                $"delete from public.\"{table}\" where false",
            };

            foreach (var sql in statements)
            {
                await using var connection = new NpgsqlConnection(_fixture.CatalogAppConnectionString);
                await connection.OpenAsync();
                await using var command = new NpgsqlCommand(sql, connection);

                try
                {
                    await command.ExecuteNonQueryAsync();
                }
                catch (PostgresException error)
                {
                    Assert.Equal("42501", error.SqlState);
                    continue;
                }

                Assert.Fail($"ACHADO DE SEGURANÇA: o acs_app EXECUTOU no catálogo: {sql}");
            }
        }
    }
}
