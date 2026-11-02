using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlSystem.Tests.Api;

// FR-030 / SC-008: a suíte FALHA quando uma rota real da Api não tem cobertura no EndpointCatalog (ou o catálogo cita uma
// rota que não existe). Assim, endpoint novo sem os testes de autorização não entra.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class EndpointCoverageTests : ApiTestBase
{
    public EndpointCoverageTests(PostgresContainerFixture database) : base(database)
    {
    }

    /// <summary>Rotas de controller da Api em execução, no formato "METHOD rota" (só <c>RouteEndpoint</c> de controller).</summary>
    private IReadOnlyList<string> RealRoutes()
    {
        _ = Client;   // sobe a Api

        return Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is not null)
            .SelectMany(endpoint =>
                (endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [])
                    .Select(method => $"{method} {endpoint.RoutePattern.RawText}"))
            .ToArray();
    }

    [Fact]
    public void Catalogo_cobre_todas_as_rotas_reais_da_api()
    {
        var diff = EndpointCatalog.Diff(RealRoutes());

        Assert.True(diff.IsEmpty, diff.ToString());
    }

    // Prova do FR-030 (cenário 5 da US7): uma rota nova, sem nenhuma regra de acesso e sem entrada no catálogo, é apontada.
    [Fact]
    public void Rota_nova_sem_cobertura_faz_a_verificacao_falhar()
    {
        Factory.WithOpenProbeEndpoint();

        var diff = EndpointCatalog.Diff(RealRoutes());

        Assert.False(diff.IsEmpty);
        Assert.Contains("GET __probe/open", diff.RealRoutesOutsideCatalog);
    }
}
