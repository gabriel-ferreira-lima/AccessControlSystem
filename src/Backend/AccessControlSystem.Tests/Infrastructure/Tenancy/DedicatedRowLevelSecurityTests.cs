using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// FR-020 / R1 a R3 na base DEDICADA. A base compartilhada já é provada por RowLevelSecurityTests (spec 01), que não é
// duplicado nem alterado: aqui está só o equivalente na outra base de tenant, com o usuário da aplicação e sem o filtro do EF.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class DedicatedRowLevelSecurityTests
{
    private readonly PostgresContainerFixture _fixture;

    public DedicatedRowLevelSecurityTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static async Task<object?> ScalarAsync(NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        return await command.ExecuteScalarAsync();
    }

    // R1
    [Fact]
    public async Task Leitura_sem_filtro_do_ef_so_ve_o_tenant_informado_na_base_dedicada()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var deA = new[]
        {
            await _fixture.SeedOperatorAsync(tenantA, dedicated: true),
            await _fixture.SeedOperatorAsync(tenantA, dedicated: true)
        };
        var deB = new[]
        {
            await _fixture.SeedOperatorAsync(tenantB, dedicated: true),
            await _fixture.SeedOperatorAsync(tenantB, dedicated: true)
        };

        await using var context = _fixture.CreateDedicatedContext(tenantA);

        // O filtro do EF é desligado de propósito (como se o código tivesse esquecido dele).
        var ids = await context.Operators.IgnoreQueryFilters().AsNoTracking().Select(x => x.Id).ToListAsync();

        Assert.Equal(deA.Select(o => o.Id).Order(), ids.Order());
        Assert.Empty(ids.Intersect(deB.Select(o => o.Id)));
    }

    // R2
    [Fact]
    public async Task Conexao_sem_tenant_nao_ve_nem_altera_linhas_na_base_dedicada()
    {
        var tenant = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenant, dedicated: true);

        await using var connection = await _fixture.OpenAppConnectionToDedicatedAsync();   // acs_app, sem nenhum tenant informado

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
            _fixture.DedicatedMigratorConnectionString,
            $"select count(*) from operators where id = '{op.Id}' and is_active = true"));
    }

    // R3
    [Fact]
    public async Task Insert_com_o_tenant_id_de_outro_tenant_e_recusado_pelo_banco_na_base_dedicada()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();

        await using var connection = await _fixture.OpenAppConnectionToDedicatedAsync();
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

    // R3
    [Fact]
    public async Task Update_nao_passa_a_linha_para_outro_tenant_na_base_dedicada()
    {
        var tenantA = PostgresContainerFixture.NewTenantId();
        var tenantB = PostgresContainerFixture.NewTenantId();
        var op = await _fixture.SeedOperatorAsync(tenantA, dedicated: true);

        await using (var connection = await _fixture.OpenAppConnectionToDedicatedAsync())
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
            _fixture.DedicatedMigratorConnectionString,
            $"select count(*) from operators where id = '{op.Id}' and tenant_id = '{tenantA}'"));
    }
}
