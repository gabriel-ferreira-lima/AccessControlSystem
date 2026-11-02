using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Fixtures;

/// <summary>Perfil de privilégios de tabela esperado: catálogo (só leitura) ou base de tenant (DML sem DELETE).</summary>
public enum ProbeProfile
{
    /// <summary>Só <c>SELECT</c>.</summary>
    Catalog,

    /// <summary><c>SELECT</c>, <c>INSERT</c> e <c>UPDATE</c> (nunca <c>DELETE</c>, Princípio IV).</summary>
    Tenant
}

/// <summary>
/// Sonda de privilégios de um papel de banco (contracts/app-role-guarantees.md, P3 e P6 a P10). Consulta o catálogo do
/// próprio PostgreSQL pela conexão do migrator, no banco dessa conexão, e devolve a lista de violações, sem repetição.
/// Cada privilégio gera um único código: <c>DELETE</c>/<c>TRUNCATE</c> → P3; <c>INSERT</c>/<c>UPDATE</c> fora do perfil
/// (só no catálogo) → P10; qualquer outro privilégio de tabela fora do perfil (<c>REFERENCES</c>, <c>TRIGGER</c>,
/// <c>MAINTAIN</c>) → P9.
/// </summary>
public static class AppRoleProbe
{
    public const string DeleteOrTruncate = "P3-delete-or-truncate";
    public const string Superuser = "P6-superuser";
    public const string CreateDatabase = "P6-createdb";
    public const string CreateRole = "P6-createrole";
    public const string Replication = "P6-replication";
    public const string BypassRls = "P6-bypassrls";
    public const string OwnsTable = "P7-owns-table";
    public const string OwnsDatabase = "P7-owns-database";
    public const string OwnsSchema = "P7-owns-schema";
    public const string MemberOfMigrator = "P7-member-of-migrator";
    public const string CreateOnSchema = "P8-create-on-schema";
    public const string ExtraTablePrivilege = "P9-extra-table-privilege";
    public const string CatalogWrite = "P10-catalog-write";

    private static readonly string[] TablePrivileges =
        ["SELECT", "INSERT", "UPDATE", "DELETE", "TRUNCATE", "REFERENCES", "TRIGGER", "MAINTAIN"];

    private static readonly HashSet<string> AllowedOnTenant = ["SELECT", "INSERT", "UPDATE"];
    private static readonly HashSet<string> AllowedOnCatalog = ["SELECT"];

    public static async Task<IReadOnlyList<string>> ListViolationsAsync(
        string migratorConnectionString, string role, ProbeProfile profile)
    {
        var violations = new List<string>();
        void Add(string code)
        {
            if (!violations.Contains(code)) violations.Add(code);
        }

        await using var connection = new NpgsqlConnection(migratorConnectionString);
        await connection.OpenAsync();

        // Atributos do papel. Um papel que não existe é erro, nunca "lista vazia" (um nome errado não pode passar a prova).
        await using (var command = new NpgsqlCommand(
            "select rolsuper, rolcreatedb, rolcreaterole, rolreplication, rolbypassrls from pg_roles where rolname = @role",
            connection))
        {
            command.Parameters.AddWithValue("role", role);
            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException($"O papel '{role}' não existe no cluster.");

            if (reader.GetBoolean(0)) Add(Superuser);
            if (reader.GetBoolean(1)) Add(CreateDatabase);
            if (reader.GetBoolean(2)) Add(CreateRole);
            if (reader.GetBoolean(3)) Add(Replication);
            if (reader.GetBoolean(4)) Add(BypassRls);
        }

        // Propriedade de tabela, banco e esquema; associação ao migrator (que permitiria SET ROLE); CREATE no esquema.
        if (await CountAsync(connection, role,
            "select count(*) from pg_tables where schemaname = 'public' and tableowner = @role") > 0) Add(OwnsTable);

        if (await CountAsync(connection, role,
            "select count(*) from pg_database where datname = current_database() and pg_get_userbyid(datdba) = @role") > 0) Add(OwnsDatabase);

        if (await CountAsync(connection, role,
            "select count(*) from pg_namespace where nspname = 'public' and pg_get_userbyid(nspowner) = @role") > 0) Add(OwnsSchema);

        if (await CountAsync(connection, role,
            $"select case when pg_has_role(@role, '{PostgresRoleBootstrap.MigratorRole}', 'member') then 1 else 0 end") > 0) Add(MemberOfMigrator);

        if (await CountAsync(connection, role,
            "select case when has_schema_privilege(@role, 'public', 'CREATE') then 1 else 0 end") > 0) Add(CreateOnSchema);

        // Privilégios por tabela do esquema public (inclui o histórico de migrations), comparados ao perfil.
        var allowed = profile == ProbeProfile.Catalog ? AllowedOnCatalog : AllowedOnTenant;
        var privileges = string.Join(", ", TablePrivileges.Select(p => $"('{p}')"));
        await using (var command = new NpgsqlCommand(
            $@"select p.privilege
               from pg_tables t
               cross join (values {privileges}) as p(privilege)
               where t.schemaname = 'public'
                 and has_table_privilege(@role, format('public.%I', t.tablename), p.privilege)",
            connection))
        {
            command.Parameters.AddWithValue("role", role);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var privilege = reader.GetString(0);
                if (allowed.Contains(privilege)) continue;

                Add(privilege switch
                {
                    "DELETE" or "TRUNCATE" => DeleteOrTruncate,
                    "INSERT" or "UPDATE" => CatalogWrite,   // só chega aqui no perfil Catalog
                    _ => ExtraTablePrivilege
                });
            }
        }

        return violations;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string role, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("role", role);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }
}
