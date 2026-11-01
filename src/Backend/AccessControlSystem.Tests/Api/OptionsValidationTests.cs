using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AccessControlSystem.Tests.Api;

// A Api falha na partida (e não na primeira requisição) quando falta configuração obrigatória.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class OptionsValidationTests : ApiTestBase
{
    public OptionsValidationTests(PostgresContainerFixture database) : base(database)
    {
    }

    // O WebApplicationFactory pode embrulhar a exceção da partida; procura a de validação na cadeia.
    private static OptionsValidationException FindValidationException(Exception exception)
    {
        static OptionsValidationException? Find(Exception? current) => current switch
        {
            null => null,
            OptionsValidationException validation => validation,
            AggregateException aggregate => aggregate.InnerExceptions.Select(Find).FirstOrDefault(x => x is not null),
            _ => Find(current.InnerException)
        };

        return Find(exception)
            ?? throw new Xunit.Sdk.XunitException($"Nenhuma OptionsValidationException na cadeia de exceções: {exception}");
    }

    [Fact]
    public void Api_nao_inicia_sem_a_chave_do_jwt()
    {
        Factory.With("Secrets:JwtPrivateKey", null);

        var exception = Assert.ThrowsAny<Exception>(() => Factory.CreateClient());

        var validation = FindValidationException(exception);
        Assert.Contains(validation.Failures, f => f.Contains("Secrets:JwtPrivateKey"));
    }

    [Fact]
    public void Api_nao_inicia_sem_a_connection_string_do_catalogo()
    {
        Factory.With("ConnectionStrings:Catalog", null);

        var exception = Assert.ThrowsAny<Exception>(() => Factory.CreateClient());

        var validation = FindValidationException(exception);
        Assert.Contains(validation.Failures, f => f.Contains("ConnectionStrings:Catalog"));
    }

    [Fact]
    public void Api_nao_inicia_com_base_configurada_sem_connection_string()
    {
        Factory.With("TenantDatabases:base_incompleta:ConnectionString", "");

        var exception = Assert.ThrowsAny<Exception>(() => Factory.CreateClient());

        var validation = FindValidationException(exception);
        Assert.Contains(validation.Failures, f => f.Contains("base_incompleta"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Api_inicia_sem_tenant_padrao_do_login(string? defaultTenantId)
    {
        Factory.With("Login:DefaultTenantId", defaultTenantId);

        // Não lança: o tenant padrão não é validado na partida (só o login é afetado).
        var response = await GetAsync("v1/account/get-me");

        Assert.Equal(401, (int)response.StatusCode);
    }

    [Fact]
    public async Task Api_nao_altera_a_estrutura_de_nenhuma_base_ao_iniciar()
    {
        // FR-020: quem aplica a estrutura é o Migrator. Uma base vazia, com a connection string do
        // DONO (que poderia criar tabelas), continua sem tabelas depois de a Api subir e atender.
        var empty = await Database.CreateEmptyDatabaseAsync($"acs_empty_{Guid.NewGuid():N}");
        const string databaseName = "base_vazia_01";
        Factory
            .WithTenantDatabase(databaseName, empty.Migrator)
            .With("ConnectionStrings:DefaultConnection", empty.Migrator);   // a chave antiga, que a Api já usou para migrar

        var tenantId = await ApiFactory.RegisterTenantAsync(Database, databaseName: databaseName);
        var admin = TenantSeed.NewOperator(role: ERole.Admin);

        // Uma requisição autenticada chega a consultar a base vazia (e falha por não haver tabela).
        var response = await GetAsync("v1/account/get-me", TestTokens.For(admin, tenantId));
        Assert.NotEqual(200, (int)response.StatusCode);

        await using var connection = new NpgsqlConnection(empty.Migrator);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog', 'information_schema')",
            connection);
        var tables = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(0, tables);
    }
}
