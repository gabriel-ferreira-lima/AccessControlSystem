using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Api;

// Princípio VII: cada um dos 8 endpoints de v1/account/* mantém a sua regra de acesso (anônimo,
// qualquer operador autenticado ou só admin), agora também com o tenant no token.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class EndpointAuthorizationTests : ApiTestBase
{
    // {id} é trocado por um Guid: a autorização acontece antes do model binding, então o valor não importa.
    private const string AnyId = "{id}";

    public EndpointAuthorizationTests(PostgresContainerFixture database) : base(database)
    {
    }

    /// <summary>Os endpoints que exigem token (todos menos o login), vindos do <see cref="EndpointCatalog"/> (hoje 7).</summary>
    public static IEnumerable<object[]> AuthenticatedEndpoints =>
        EndpointCatalog.Entries
            .Where(entry => entry.Access != EndpointAccess.Anonymous)
            .Select(entry => new object[] { entry.Method, entry.Route });

    /// <summary>Os endpoints que só o admin pode chamar, vindos do <see cref="EndpointCatalog"/> (hoje 5).</summary>
    public static IEnumerable<object[]> AdminEndpoints =>
        EndpointCatalog.Entries
            .Where(entry => entry.Access == EndpointAccess.AdminOnly)
            .Select(entry => new object[] { entry.Method, entry.Route });

    private Task<HttpResponseMessage> CallAsync(string method, string route, string? token) =>
        SendAsync(new HttpMethod(method), route.Replace(AnyId, Guid.NewGuid().ToString()), token, body: new { });

    [Theory]
    [MemberData(nameof(AuthenticatedEndpoints))]
    public async Task Sem_token_retorna_401(string method, string route)
    {
        var envelope = await ReadAsync(await CallAsync(method, route, token: null));

        Assert.Equal(401, envelope.HttpStatus);
        Assert.Equal("Não autenticado", envelope.Message);
    }

    [Fact]
    public async Task Login_continua_anonimo()
    {
        var (tenantId, @operator) = await TenantWithOperatorAsync(ERole.Operator);
        Factory.WithDefaultTenant(tenantId);

        var response = await LoginAsync(@operator.Email, OperatorPassword);

        Assert.Equal(200, (int)response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Role_Operator_em_endpoint_de_admin_retorna_403(string method, string route)
    {
        var (tenantId, @operator) = await TenantWithOperatorAsync(ERole.Operator);

        var envelope = await ReadAsync(await CallAsync(method, route, TestTokens.For(@operator, tenantId)));

        Assert.Equal(403, envelope.HttpStatus);
        Assert.Equal("Sem permissão", envelope.Message);
    }

    [Theory]
    [MemberData(nameof(AuthenticatedEndpoints))]
    public async Task Token_de_outro_tenant_retorna_401(string method, string route)
    {
        var (_, admin) = await TenantWithOperatorAsync();
        var (otherTenant, _) = await TenantWithOperatorAsync();

        // Admin do tenant A com a claim do tenant B: nenhum endpoint o aceita.
        var envelope = await ReadAsync(await CallAsync(method, route, TestTokens.For(admin, otherTenant)));

        Assert.Equal(401, envelope.HttpStatus);
        Assert.Equal("Não autenticado", envelope.Message);
    }

    [Theory]
    [MemberData(nameof(AdminEndpoints))]
    public async Task Admin_do_proprio_tenant_passa_pela_autorizacao(string method, string route)
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        var response = await CallAsync(method, route, TestTokens.For(admin, tenantId));

        // O resultado depende do endpoint (400, 404, 201...), mas nunca é 401 nem 403.
        Assert.NotEqual(401, (int)response.StatusCode);
        Assert.NotEqual(403, (int)response.StatusCode);
    }

    [Theory]
    [InlineData("não é um jwt")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.e30.assinatura-invalida")]
    public async Task Login_ignora_token_enviado_e_continua_anonimo(string bearer)
    {
        var (tenantId, @operator) = await TenantWithOperatorAsync(ERole.Operator);
        Factory.WithDefaultTenant(tenantId);

        // Um token (inválido, no caso) enviado ao login é ignorado: nunca o barra.
        var response = await LoginAsync(@operator.Email, OperatorPassword, bearerToken: bearer);

        Assert.Equal(200, (int)response.StatusCode);
    }

    #region Token emitido ANTES de uma troca (FR-004 a FR-006, FR-029): todo endpoint autenticado o recusa

    private async Task AssertTokenAntigoRecusadoAsync(
        string method, string route, Func<Guid, Guid, Task> change)
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();
        var tokenAntigo = TestTokens.For(admin, tenantId);   // emitido antes da mudança

        await change(tenantId, admin.Id);                    // mudança pelo MÉTODO DE DOMÍNIO

        var envelope = await ReadAsync(await CallAsync(method, route, tokenAntigo));

        Assert.Equal(401, envelope.HttpStatus);
        Assert.Equal("Não autenticado", envelope.Message);
    }

    [Theory]
    [MemberData(nameof(AuthenticatedEndpoints))]
    public Task Token_antigo_depois_de_troca_de_senha_retorna_401(string method, string route) =>
        AssertTokenAntigoRecusadoAsync(method, route,
            (tenantId, operatorId) => Database.ChangePasswordAsync(tenantId, operatorId));

    [Theory]
    [MemberData(nameof(AuthenticatedEndpoints))]
    public Task Token_antigo_depois_de_troca_de_perfil_retorna_401(string method, string route) =>
        AssertTokenAntigoRecusadoAsync(method, route,
            (tenantId, operatorId) => Database.ChangeRoleAsync(tenantId, operatorId, ERole.Operator));

    [Theory]
    [MemberData(nameof(AuthenticatedEndpoints))]
    public Task Token_antigo_depois_de_desativacao_retorna_401(string method, string route) =>
        AssertTokenAntigoRecusadoAsync(method, route,
            (tenantId, operatorId) => Database.DeactivateAsync(tenantId, operatorId));

    #endregion
}
