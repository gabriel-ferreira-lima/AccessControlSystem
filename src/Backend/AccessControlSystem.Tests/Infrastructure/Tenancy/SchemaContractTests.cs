using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// Contrato do esquema de tenant (contracts/database-isolation.md, garantias G5, G7 e G8).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class SchemaContractTests
{
    private readonly PostgresContainerFixture _fixture;

    public SchemaContractTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private Task<long> Count(string sql) => DatabaseProbe.CountAsync(_fixture.SharedMigratorConnectionString, sql);

    [Fact]
    public async Task Papel_da_aplicacao_nao_tem_privilegios_alem_de_dml()
    {
        Assert.Equal(1L, await Count("select count(*) from (select has_table_privilege('acs_app', 'operators', 'SELECT') and has_table_privilege('acs_app', 'operators', 'INSERT') and has_table_privilege('acs_app', 'operators', 'UPDATE') as ok) t where ok"));
        Assert.Equal(0L, await Count("select count(*) from (select has_table_privilege('acs_app', 'operators', 'DELETE') as tem) t where tem"));
        Assert.Equal(0L, await Count("select count(*) from (select has_schema_privilege('acs_app', 'public', 'CREATE') as tem) t where tem"));

        // Na prática: DDL como acs_app é negado.
        await using var connection = await _fixture.OpenAppConnectionAsync();
        await using var ddl = new NpgsqlCommand("create table intruso (a int)", connection);
        var erro = await Assert.ThrowsAsync<PostgresException>(() => ddl.ExecuteNonQueryAsync());
        Assert.Equal("42501", erro.SqlState);
    }

    [Fact]
    public async Task Papel_da_aplicacao_nao_e_dono_e_nao_tem_bypassrls()
    {
        Assert.Equal(0L, await Count("select count(*) from pg_roles where rolname = 'acs_app' and (rolbypassrls or rolsuper or rolcreatedb or rolcreaterole)"));
        Assert.Equal(1L, await Count("select count(*) from pg_tables where tablename = 'operators' and tableowner = 'acs_migrator'"));
    }

    [Fact]
    public async Task Coluna_tenant_id_e_obrigatoria()
    {
        Assert.Equal(1L, await Count(
            "select count(*) from information_schema.columns where table_name = 'operators' and column_name = 'tenant_id' and is_nullable = 'NO' and data_type = 'uuid'"));
    }

    [Fact]
    public async Task Indices_do_operador_existem()
    {
        var definicoes = await DatabaseProbe.ReadStringsAsync(
            _fixture.SharedMigratorConnectionString,
            "select indexname || '|' || indexdef from pg_indexes where tablename = 'operators'");

        var porEmail = Assert.Single(definicoes, d => d.StartsWith("ix_operators_tenant_id_email|", StringComparison.Ordinal));
        Assert.Contains("UNIQUE", porEmail);
        Assert.Contains("(tenant_id, email)", porEmail);

        var porId = Assert.Single(definicoes, d => d.StartsWith("ix_operators_tenant_id_id|", StringComparison.Ordinal));
        Assert.Contains("(tenant_id, id)", porId);

        // O índice único global do e-mail deixou de existir.
        Assert.DoesNotContain(definicoes, d => d.StartsWith("IX_operators_email|", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Toda_tabela_de_tenant_tem_rls_e_politica()
    {
        await using var context = _fixture.CreateContext(PostgresContainerFixture.NewTenantId());

        var tabelas = context.Model.GetEntityTypes()
            .Where(t => !t.IsOwned())
            .Select(t => t.GetTableName())
            .Where(nome => nome is not null)
            .Distinct()
            .ToList();

        Assert.NotEmpty(tabelas);
        foreach (var tabela in tabelas)
        {
            Assert.Equal(1L, await Count(
                $"select count(*) from pg_class where relname = '{tabela}' and relrowsecurity and relforcerowsecurity"));
            Assert.Equal(1L, await Count(
                $"select count(*) from pg_policies where tablename = '{tabela}' and policyname = '{tabela}_tenant_isolation'"));
        }
    }

    [Fact]
    public async Task Email_e_unico_por_tenant()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var email = TenantSeed.UniqueEmail("unico");
        await _fixture.SeedOperatorAsync(tenant, email);

        await using var context = _fixture.CreateContext(tenant);
        context.Operators.Add(TenantSeed.NewOperator(email));

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        Assert.Equal("23505", Assert.IsType<PostgresException>(erro.InnerException).SqlState);
    }
}
