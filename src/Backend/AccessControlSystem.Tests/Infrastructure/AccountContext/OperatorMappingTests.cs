using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

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
}
