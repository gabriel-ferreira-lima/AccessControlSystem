using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AuthRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate.Repository;
using CreateRepository = AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Create.Repository;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Authenticate;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class AuthenticateRepositoryTests
{
    private readonly PostgresContainerFixture _fixture;

    public AuthenticateRepositoryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private static string UniqueEmail() => $"auth-{Guid.NewGuid():N}@test.com";

    [Fact]
    public async Task GetOperatorByEmailAsync_null_quando_nao_existe()
    {
        await using var context = _fixture.CreateContext();

        Assert.Null(await new AuthRepository(context).GetOperatorByEmailAsync(UniqueEmail()));
    }

    [Fact]
    public async Task GetOperatorByEmailAsync_devolve_o_operador_com_a_senha_utilizavel()
    {
        var email = UniqueEmail();

        await using (var seed = _fixture.CreateContext())
            await new CreateRepository(seed).SaveAsync(
                new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Admin));

        await using var context = _fixture.CreateContext();
        var op = await new AuthRepository(context).GetOperatorByEmailAsync(email);

        Assert.NotNull(op);
        Assert.Equal(email, op!.Email.Address);
        Assert.Equal(ERole.Admin, op.Role);
        Assert.True(op.Password.Verify("Str0ng!Pass"));
    }

    // MUDANÇA DE EXPECTATIVA POR MUDANÇA DE SPEC (FR-030/FR-031): este teste se chamava
    // Admin_semeado_pela_migration_existe e afirmava o contrário. Agora uma base NOVA, migrada pelo
    // Migrator, não recebe o administrador de e-mail e senha fixos (a SeedAdmin é pré-marcada como
    // aplicada). O admin de desenvolvimento nasce só pelo seed-dev, que roda apenas em Development.
    [Fact]
    public async Task Base_nova_nao_recebe_o_admin_de_seed()
    {
        await using var context = _fixture.CreateContext();

        Assert.Null(await new AuthRepository(context).GetOperatorByEmailAsync("admin@admin.com.br"));

        // Em nenhum tenant da base de teste (conferência pelo migrator, que enxerga todos).
        Assert.Equal(0L, await DatabaseProbe.CountAsync(
            _fixture.SharedMigratorConnectionString,
            "select count(*) from operators where email = 'admin@admin.com.br'"));
    }

    [Fact]
    public async Task Isolamento_nao_devolve_o_mesmo_email_de_outro_tenant()
    {
        var email = TenantSeed.UniqueEmail("auth-iso");
        await _fixture.SeedOperatorAsync(PostgresContainerFixture.NewTenantId(), email);

        await using var context = _fixture.CreateContext();   // tenant padrão da fixture

        Assert.Null(await new AuthRepository(context).GetOperatorByEmailAsync(email));
    }
}
