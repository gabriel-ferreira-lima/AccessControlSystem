using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.IdentityModel.Tokens;

namespace AccessControlSystem.Tests.Api;

// FR-010 a FR-013: a Api só aceita token emitido por este sistema (emissor) para esta aplicação (destinatário),
// assinado em HS256, dentro da validade e de uma tolerância de relógio curta (30 s).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class JwtValidationTests : ApiTestBase
{
    private const string GetMe = "v1/account/get-me";

    public JwtValidationTests(PostgresContainerFixture database) : base(database)
    {
    }

    /// <summary>Token do admin do próprio tenant, com cada claim de validação ajustável.</summary>
    private static string Token(
        Operator admin,
        Guid tenantId,
        string? issuer = ApiFactory.JwtIssuer,
        string? audience = ApiFactory.JwtAudience,
        DateTime? expires = null,
        string? algorithm = SecurityAlgorithms.HmacSha256Signature) =>
        TestTokens.Create(admin.Id.ToString(), admin.Email, admin.Role, tenantId.ToString(), issuer, audience, expires, algorithm);

    private static async Task AssertNaoAutenticadoAsync(Task<HttpResponseMessage> call)
    {
        var envelope = await ReadAsync(await call);

        Assert.Equal(401, envelope.HttpStatus);
        Assert.Equal("Não autenticado", envelope.Message);
    }

    #region Emissor e destinatário

    [Fact]
    public async Task Token_com_emissor_diferente_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        await AssertNaoAutenticadoAsync(GetAsync(GetMe, Token(admin, tenantId, issuer: "outro-sistema")));
    }

    [Fact]
    public async Task Token_sem_emissor_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        await AssertNaoAutenticadoAsync(GetAsync(GetMe, Token(admin, tenantId, issuer: null)));
    }

    [Fact]
    public async Task Token_com_destinatario_diferente_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        await AssertNaoAutenticadoAsync(GetAsync(GetMe, Token(admin, tenantId, audience: "outra-aplicacao")));
    }

    [Fact]
    public async Task Token_sem_destinatario_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        await AssertNaoAutenticadoAsync(GetAsync(GetMe, Token(admin, tenantId, audience: null)));
    }

    #endregion

    #region Validade e tolerância de relógio

    [Fact]
    public async Task Token_vencido_ha_45_segundos_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        await AssertNaoAutenticadoAsync(
            GetAsync(GetMe, Token(admin, tenantId, expires: DateTime.UtcNow.AddSeconds(-45))));
    }

    // Guarda: dentro da tolerância de 30 s o token ainda vale (já passa hoje, com a tolerância padrão maior).
    [Fact]
    public async Task Token_vencido_ha_10_segundos_ainda_e_aceito()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        var response = await GetAsync(GetMe, Token(admin, tenantId, expires: DateTime.UtcNow.AddSeconds(-10)));

        Assert.Equal(200, (int)response.StatusCode);
    }

    #endregion

    #region Algoritmo e assinatura

    [Fact]
    public async Task Token_assinado_com_hs384_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        // Mesma chave, outro algoritmo HMAC: o algoritmo precisa ser fixado em HS256.
        await AssertNaoAutenticadoAsync(
            GetAsync(GetMe, Token(admin, tenantId, algorithm: SecurityAlgorithms.HmacSha384Signature)));
    }

    // Guarda: token sem assinatura ("alg": "none") já é recusado hoje.
    [Fact]
    public async Task Token_sem_assinatura_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        await AssertNaoAutenticadoAsync(GetAsync(GetMe, Token(admin, tenantId, algorithm: null)));
    }

    #endregion

    #region Caminho feliz

    // Este é o único teste que usa o token de um LOGIN REAL (os demais usam token fabricado pelo TestTokens): prova que
    // o TokenService emite emissor e destinatário aceitos pela validação estrita.
    [Fact]
    public async Task Token_emitido_pelo_login_e_aceito_nas_chamadas_seguintes()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(tenantId);

        var login = await ReadAsync(await LoginAsync(admin.Email, OperatorPassword));
        Assert.Equal(200, login.HttpStatus);
        var token = login.Data.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var response = await GetAsync(GetMe, token);

        Assert.Equal(200, (int)response.StatusCode);
    }

    #endregion
}
