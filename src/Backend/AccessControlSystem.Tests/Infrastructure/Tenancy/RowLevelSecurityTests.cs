using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// Segunda camada do Princípio I: o isolamento vale NO BANCO, mesmo que o código esqueça o filtro do EF.
// As garantias G1 a G4 e G6 de contracts/database-isolation.md.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class RowLevelSecurityTests
{
    private readonly PostgresContainerFixture _fixture;

    public RowLevelSecurityTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteScalarAsync();
    }

    [Fact]
    public async Task Rls_bloqueia_mesmo_sem_o_filtro_do_ef()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var deA = new[] { await _fixture.SeedOperatorAsync(tenantA), await _fixture.SeedOperatorAsync(tenantA) };
        var deB = new[] { await _fixture.SeedOperatorAsync(tenantB), await _fixture.SeedOperatorAsync(tenantB) };

        await using var context = _fixture.CreateContext(tenantA);

        // O filtro do EF é desligado de propósito (como se o código tivesse esquecido dele).
        var ids = await context.Operators.IgnoreQueryFilters().AsNoTracking().Select(x => x.Id).ToListAsync();

        Assert.Equal(deA.Select(o => o.Id).Order(), ids.Order());
        Assert.Empty(ids.Intersect(deB.Select(o => o.Id)));
    }

    [Fact]
    public async Task Conexao_sem_tenant_nao_ve_nem_altera_linhas()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenant);

        await using var connection = await _fixture.OpenAppConnectionAsync();   // acs_app, sem nenhum tenant informado

        Assert.Equal(0L, await ScalarAsync(connection, "select count(*) from operators"));

        await using (var update = new NpgsqlCommand("update operators set is_active = false", connection))
            Assert.Equal(0, await update.ExecuteNonQueryAsync());   // 0 linhas afetadas, sem erro

        // Nem inserir: sem tenant, o WITH CHECK recusa.
        await using var insert = new NpgsqlCommand(
            "insert into operators (id, email, password, role, tenant_id) values (@id, @email, 'x', 1, @tenant)", connection);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("email", TenantSeed.UniqueEmail("sem-tenant"));
        insert.Parameters.AddWithValue("tenant", tenant);
        var erro = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
        Assert.Equal("42501", erro.SqlState);

        // A linha original continua intacta (conferência pelo migrator).
        Assert.Equal(1L, await DatabaseProbe.CountAsync(
            _fixture.SharedMigratorConnectionString,
            $"select count(*) from operators where id = '{op.Id}' and is_active = true"));
    }

    [Fact]
    public async Task Insert_de_outro_tenant_e_recusado_pelo_banco()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();

        await using var connection = await _fixture.OpenAppConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await ScalarAsync(connection, $"select set_config('app.tenant_id', '{tenantA}', true)", transaction);

        await using var insert = new NpgsqlCommand(
            "insert into operators (id, email, password, role, tenant_id) values (@id, @email, 'x', 1, @tenant)",
            connection, transaction);
        insert.Parameters.AddWithValue("id", Guid.NewGuid());
        insert.Parameters.AddWithValue("email", TenantSeed.UniqueEmail("invasor"));
        insert.Parameters.AddWithValue("tenant", tenantB);   // tenant diferente do corrente

        var erro = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());

        Assert.Equal("42501", erro.SqlState);
    }

    [Fact]
    public async Task Update_nao_altera_o_tenant_id()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenantA);

        await using (var connection = await _fixture.OpenAppConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await ScalarAsync(connection, $"select set_config('app.tenant_id', '{tenantA}', true)", transaction);

            // Tentar passar a linha do tenant A para o B: a linha nova violaria o WITH CHECK.
            await using var update = new NpgsqlCommand(
                $"update operators set tenant_id = '{tenantB}' where id = '{op.Id}'", connection, transaction);
            var erro = await Assert.ThrowsAsync<PostgresException>(() => update.ExecuteNonQueryAsync());
            Assert.Equal("42501", erro.SqlState);
        }

        Assert.Equal(1L, await DatabaseProbe.CountAsync(
            _fixture.SharedMigratorConnectionString,
            $"select count(*) from operators where id = '{op.Id}' and tenant_id = '{tenantA}'"));
    }

    [Fact]
    public async Task Valor_do_tenant_nao_sobrevive_a_transacao()
    {
        var tenant = PostgresContainerFixture.NewTenantId();

        await using var connection = await _fixture.OpenAppConnectionAsync();
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await ScalarAsync(connection, $"select set_config('app.tenant_id', '{tenant}', true)", transaction);
            Assert.Equal(tenant.ToString(), await ScalarAsync(connection, "select current_setting('app.tenant_id', true)", transaction));
            await transaction.CommitAsync();
        }

        var depois = await ScalarAsync(connection, "select current_setting('app.tenant_id', true)");
        Assert.True(depois is null || depois is DBNull || (depois is string texto && texto.Length == 0),
            $"O app.tenant_id sobreviveu à transação: '{depois}'.");
    }
}
