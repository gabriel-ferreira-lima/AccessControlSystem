using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext;

// Valida o OperatorMap contra um Postgres real: owned types, conversão do enum e índice único.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class OperatorMappingTests
{
    private readonly PostgresContainerFixture _fixture;

    public OperatorMappingTests(PostgresContainerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Operador_faz_round_trip_completo()
    {
        var email = $"map-{Guid.NewGuid():N}@test.com";
        Guid id;

        await using (var context = _fixture.CreateContext())
        {
            var op = new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Operator);
            context.Operators.Add(op);
            await context.SaveChangesAsync();
            id = op.Id;
        }

        await using (var context = _fixture.CreateContext())
        {
            var op = await context.Operators.AsNoTracking().SingleAsync(o => o.Email.Address == email);

            Assert.Equal(id, op.Id);
            Assert.Equal(email, op.Email.Address);
            Assert.Equal(ERole.Operator, op.Role);
            Assert.True(op.Password.Verify("Str0ng!Pass"));
        }
    }

    [Fact]
    public async Task Indice_unico_no_email_e_aplicado_pelo_banco()
    {
        var email = $"uniq-{Guid.NewGuid():N}@test.com";

        await using var context = _fixture.CreateContext();

        context.Operators.Add(new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Operator));
        await context.SaveChangesAsync();

        context.Operators.Add(new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Operator));
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SecurityVersion_faz_round_trip()
    {
        // Grava com a versão já incrementada, lê, troca a senha pela entidade, salva e lê de novo.
        var email = $"ver-{Guid.NewGuid():N}@test.com";
        Guid id;

        await using (var context = _fixture.CreateContext())
        {
            var op = new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Operator);
            op.UpdatePassword(new Password("Nova1!Senha"));
            context.Operators.Add(op);
            await context.SaveChangesAsync();
            id = op.Id;
        }

        await using (var context = _fixture.CreateContext())
        {
            var op = await context.Operators.SingleAsync(o => o.Id == id);
            Assert.Equal(1, op.SecurityVersion);

            op.UpdatePassword(new Password("Outra1!Senha"));
            await context.SaveChangesAsync();
        }

        await using (var context = _fixture.CreateContext())
        {
            var op = await context.Operators.AsNoTracking().SingleAsync(o => o.Id == id);
            Assert.Equal(2, op.SecurityVersion);
        }
    }

    [Fact]
    public async Task Operador_gravado_por_sql_sem_a_coluna_le_security_version_zero()
    {
        // A coluna tem DEFAULT 0: uma linha gravada por quem não conhece a coluna (a versão anterior da Api) lê 0.
        var tenant = PostgresContainerFixture.NewTenantId();
        var id = Guid.NewGuid();
        var email = TenantSeed.UniqueEmail("sem-coluna");

        await using (var connection = await _fixture.OpenAppConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using (var setTenant = new NpgsqlCommand($"select set_config('app.tenant_id', '{tenant}', true)", connection, transaction))
                await setTenant.ExecuteScalarAsync();

            await using var insert = new NpgsqlCommand(
                "insert into operators (id, email, password, role, tenant_id) values (@id, @email, 'x', 1, @tenant)",
                connection, transaction);
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("email", email);
            insert.Parameters.AddWithValue("tenant", tenant);
            await insert.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
        }

        await using var context = _fixture.CreateContext(tenant);
        var op = await context.Operators.AsNoTracking().SingleAsync(o => o.Id == id);

        Assert.Equal(0, op.SecurityVersion);
    }
}
