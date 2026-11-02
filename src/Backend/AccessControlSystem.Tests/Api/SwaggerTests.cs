using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;

namespace AccessControlSystem.Tests.Api;

// FR-003: a documentação interativa (Swagger) e o arquivo que a descreve só existem em Development.
// Fora dele, as rotas respondem 404 como se não existissem. O ambiente padrão dos testes é "Testing".
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class SwaggerTests : ApiTestBase
{
    public SwaggerTests(PostgresContainerFixture database) : base(database)
    {
    }

    [Theory]
    [InlineData("Testing", "swagger/v1/swagger.json")]
    [InlineData("Testing", "swagger/index.html")]
    [InlineData("Production", "swagger/v1/swagger.json")]
    [InlineData("Production", "swagger/index.html")]
    public async Task Swagger_fora_de_development_responde_404(string environment, string route)
    {
        Factory.WithEnvironment(environment);

        var response = await GetAsync(route);

        Assert.Equal(404, (int)response.StatusCode);
    }

    [Fact]
    public async Task Operador_autenticado_tambem_recebe_404_no_swagger_fora_de_development()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        var response = await GetAsync("swagger/v1/swagger.json", TestTokens.For(admin, tenantId));

        Assert.Equal(404, (int)response.StatusCode);
    }

    // Guarda: já passa hoje. Prova que o 404 acima vem da condição de ambiente e não de o Swagger estar quebrado.
    [Theory]
    [InlineData("swagger/v1/swagger.json")]
    [InlineData("swagger/index.html")]
    public async Task Swagger_em_development_continua_disponivel(string route)
    {
        Factory.WithEnvironment("Development");

        var response = await GetAsync(route);

        Assert.Equal(200, (int)response.StatusCode);
    }
}
