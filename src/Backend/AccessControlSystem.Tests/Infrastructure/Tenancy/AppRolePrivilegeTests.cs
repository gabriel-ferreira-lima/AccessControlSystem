using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Npgsql;
using Xunit.Abstractions;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// FR-019 e FR-021: com o usuário da aplicação (acs_app), nas três bases (catálogo, compartilhada e dedicada), nenhuma
// instrução de estrutura, de propriedade, de papel ou de isolamento funciona, e a sonda de privilégios não acusa nada.
// Se uma instrução FUNCIONAR, é um achado de segurança (o acs_app tem permissão a mais), não um defeito do teste.
// Contrato: contracts/app-role-guarantees.md (P1 a P9). A base compartilhada também é coberta por SchemaContractTests
// (matriz do operators, CREATE TABLE); aqui a diferença é a parametrização por base e a cobertura de abuso.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class AppRolePrivilegeTests
{
    public enum DbKind
    {
        Catalog,
        Shared,
        Dedicated
    }

    private const string InsufficientPrivilege = "42501";

    private readonly PostgresContainerFixture _fixture;
    private readonly ITestOutputHelper _output;

    public AppRolePrivilegeTests(PostgresContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    private (string App, string Migrator, ProbeProfile Profile, string Table) Target(DbKind kind) => kind switch
    {
        DbKind.Catalog => (_fixture.CatalogAppConnectionString, _fixture.CatalogMigratorConnectionString, ProbeProfile.Catalog, "tenants"),
        DbKind.Shared => (_fixture.SharedAppConnectionString, _fixture.SharedMigratorConnectionString, ProbeProfile.Tenant, "operators"),
        DbKind.Dedicated => (_fixture.DedicatedAppConnectionString, _fixture.DedicatedMigratorConnectionString, ProbeProfile.Tenant, "operators"),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    [Theory]
    [InlineData(DbKind.Catalog)]
    [InlineData(DbKind.Shared)]
    [InlineData(DbKind.Dedicated)]
    public async Task Sonda_de_privilegios_nao_acusa_violacao_do_acs_app_na_base(DbKind kind)
    {
        var target = Target(kind);

        var violations = await AppRoleProbe.ListViolationsAsync(target.Migrator, PostgresRoleBootstrap.AppRole, target.Profile);

        Assert.True(violations.Count == 0, "Violações de privilégio do acs_app: " + string.Join(", ", violations));
    }

    // Instruções comuns às três bases; as de RLS só existem nas bases de tenant.
    private static readonly string[] CommonStatements =
    [
        "create_table", "create_index", "create_schema", "add_column", "drop_column", "drop_table", "drop_index",
        "truncate", "delete", "alter_role_bypassrls", "alter_role_superuser", "create_role", "set_role",
        "set_session_authorization"
    ];

    private static readonly string[] TenantOnlyStatements = ["disable_rls", "no_force_rls", "drop_policy"];

    public static IEnumerable<object[]> ForbiddenStatements()
    {
        foreach (var statement in CommonStatements)
            foreach (var kind in Enum.GetValues<DbKind>())
                yield return [kind, statement];

        foreach (var statement in TenantOnlyStatements)
            foreach (var kind in new[] { DbKind.Shared, DbKind.Dedicated })
                yield return [kind, statement];
    }

    // A tabela vem das constantes de Target (nunca de entrada externa); coluna e índice reais são lidos pelo migrator.
    private static async Task<string> FirstAsync(string migrator, string sql) =>
        (await DatabaseProbe.ReadStringsAsync(migrator, sql)).First();

    private static Task<string> ColumnOfAsync(string migrator, string table) => FirstAsync(migrator,
        $"select column_name from information_schema.columns where table_schema = 'public' and table_name = '{table}' order by ordinal_position desc limit 1");

    private static Task<string> IndexOfAsync(string migrator, string table) => FirstAsync(migrator,
        $"select indexname from pg_indexes where schemaname = 'public' and tablename = '{table}' order by indexname limit 1");

    private static async Task<string> SqlForAsync(string statement, string table, string migrator)
    {
        return statement switch
        {
            "create_table" => "create table intruso (a int)",
            "create_index" => $"create index intruso_ix on {table} (\"{await ColumnOfAsync(migrator, table)}\")",
            "create_schema" => "create schema intruso",
            "add_column" => $"alter table {table} add column intruso int",
            "drop_column" => $"alter table {table} drop column \"{await ColumnOfAsync(migrator, table)}\"",
            "drop_table" => $"drop table {table}",
            "drop_index" => $"drop index \"{await IndexOfAsync(migrator, table)}\"",
            "truncate" => $"truncate {table}",
            "delete" => $"delete from {table}",
            "alter_role_bypassrls" => $"alter role {PostgresRoleBootstrap.AppRole} bypassrls",
            "alter_role_superuser" => $"alter role {PostgresRoleBootstrap.AppRole} superuser",
            "create_role" => $"create role intruso_{Guid.NewGuid():N}",
            "set_role" => $"set role {PostgresRoleBootstrap.MigratorRole}",
            "set_session_authorization" => $"set session authorization {PostgresRoleBootstrap.MigratorRole}",
            "disable_rls" => $"alter table {table} disable row level security",
            "no_force_rls" => $"alter table {table} no force row level security",
            "drop_policy" => $"drop policy {table}_tenant_isolation on {table}",
            _ => throw new ArgumentOutOfRangeException(nameof(statement), statement, null)
        };
    }

    [Theory]
    [MemberData(nameof(ForbiddenStatements))]
    public async Task Instrucao_proibida_e_negada_ao_acs_app_com_permissao_insuficiente(DbKind kind, string statement)
    {
        var target = Target(kind);
        var sql = await SqlForAsync(statement, target.Table, target.Migrator);

        // Conexão nova por instrução: a transação aborta no erro.
        await using var connection = new NpgsqlConnection(target.App);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (PostgresException error)
        {
            // Tabela de SQLSTATE por instrução e por base (lida do log detalhado do teste).
            _output.WriteLine($"SQLSTATE|{kind}|{statement}|{error.SqlState}|{error.MessageText}");
            Assert.Equal(InsufficientPrivilege, error.SqlState);
            return;
        }

        Assert.Fail($"ACHADO DE SEGURANÇA: o acs_app EXECUTOU na base {kind} a instrução proibida '{statement}': {sql}");
    }
}
