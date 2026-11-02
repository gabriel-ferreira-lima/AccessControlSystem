using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlSystem.Tests.Api;

// FR-001 / FR-002: toda rota exige usuário autenticado por padrão; só o login é anônimo. Uma rota nova
// sem nenhuma regra de acesso não pode nascer aberta. A "rota de prova" (OpenProbeController) não tem
// atributo de acesso nenhum e só existe quando a fábrica pede.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class DefaultAuthorizationTests : ApiTestBase
{
    private const string ProbeRoute = "__probe/open";

    public DefaultAuthorizationTests(PostgresContainerFixture database) : base(database)
    {
    }

    [Fact]
    public async Task Rota_sem_regra_de_acesso_responde_401_sem_token()
    {
        Factory.WithOpenProbeEndpoint();

        var envelope = await ReadAsync(await GetAsync(ProbeRoute));

        Assert.Equal(401, envelope.HttpStatus);
        Assert.Equal("Não autenticado", envelope.Message);
    }

    [Fact]
    public async Task Rota_sem_regra_de_acesso_responde_200_com_token_valido()
    {
        Factory.WithOpenProbeEndpoint();
        var (tenantId, admin) = await TenantWithOperatorAsync();

        var response = await GetAsync(ProbeRoute, TestTokens.For(admin, tenantId));

        Assert.Equal(200, (int)response.StatusCode);
    }

    [Fact]
    public async Task Politica_de_fallback_exige_usuario_autenticado()
    {
        _ = Client;   // sobe a Api

        var provider = Factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallback = await provider.GetFallbackPolicyAsync();

        Assert.NotNull(fallback);
        Assert.Contains(fallback!.Requirements, requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task Rota_inexistente_responde_404_e_nao_401()
    {
        // A política de fallback só vale para endpoints que existem: rota desconhecida continua 404.
        var response = await GetAsync("v1/rota-que-nao-existe");

        Assert.Equal(404, (int)response.StatusCode);
    }
}
