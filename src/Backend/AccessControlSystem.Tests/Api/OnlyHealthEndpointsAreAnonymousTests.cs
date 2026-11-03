using AccessControlSystem.Api.Health;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AccessControlSystem.Tests.Api;

// FR-039: os endpoints de saúde são os ÚNICOS novos endpoints anônimos e os únicos sem controller. Mesmo padrão do
// EndpointCoverageTests: lê o EndpointDataSource da Api em execução (ambiente Testing, então não há Swagger).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class OnlyHealthEndpointsAreAnonymousTests : ApiTestBase
{
    private const string Login = "v1/account/login";

    private static readonly string[] RotasDeSaude = ["health/live", "health/ready"];

    public OnlyHealthEndpointsAreAnonymousTests(PostgresContainerFixture database) : base(database)
    {
    }

    private static string Rota(RouteEndpoint endpoint) => (endpoint.RoutePattern.RawText ?? string.Empty).TrimStart('/');

    private IReadOnlyList<RouteEndpoint> Endpoints()
    {
        _ = Client;   // sobe a Api

        return Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToArray();
    }

    [Fact]
    public void Os_unicos_endpoints_sem_controller_sao_as_duas_rotas_de_saude()
    {
        var semController = Endpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>() is null)
            .Select(Rota)
            .Order()
            .ToArray();

        Assert.Equal(RotasDeSaude, semController);
    }

    [Fact]
    public void Os_unicos_endpoints_anonimos_sao_o_login_e_as_duas_rotas_de_saude()
    {
        var anonimos = Endpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Rota)
            .Order()
            .ToArray();

        Assert.Equal(new[] { "health/live", "health/ready", Login }, anonimos);
    }

    // Sanidade: se o conjunto vier vazio ou sem o login, o teste não estaria enxergando os endpoints da Api.
    [Fact]
    public void Sanidade_o_conjunto_de_anonimos_inclui_o_login_e_as_duas_rotas_de_saude()
    {
        var anonimos = Endpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(Rota)
            .ToArray();

        Assert.NotEmpty(anonimos);
        Assert.Contains(Login, anonimos);
        Assert.All(RotasDeSaude, rota => Assert.Contains(rota, anonimos));
    }

    [Fact]
    public void As_rotas_de_saude_ficam_fora_de_v1()
    {
        var saude = Endpoints().Where(endpoint => RotasDeSaude.Contains(Rota(endpoint))).ToArray();

        Assert.Equal(2, saude.Length);
        Assert.All(saude, endpoint => Assert.False(Rota(endpoint).StartsWith("v1/", StringComparison.Ordinal)));
    }

    [Fact]
    public void As_rotas_de_saude_aceitam_so_GET_e_HEAD()
    {
        var saude = Endpoints().Where(endpoint => RotasDeSaude.Contains(Rota(endpoint))).ToArray();

        Assert.Equal(2, saude.Length);
        Assert.All(saude, endpoint =>
        {
            var metodos = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
            Assert.Equal(new[] { "GET", "HEAD" }, metodos.Order(StringComparer.Ordinal).ToArray());
        });
    }

    // U2: o marcador que faz o JWT ignorar o Authorization existe SÓ nas duas rotas de saúde. O comportamento do
    // resto da Api (token válido aceito, inválido recusado) é provado no HealthEndpointsTests.
    [Fact]
    public void So_as_rotas_de_saude_carregam_o_marcador_que_faz_o_jwt_ignorar_o_authorization()
    {
        var comMarcador = Endpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<HealthEndpointMarker>() is not null)
            .Select(Rota)
            .Order()
            .ToArray();

        Assert.Equal(RotasDeSaude, comMarcador);
    }
}
