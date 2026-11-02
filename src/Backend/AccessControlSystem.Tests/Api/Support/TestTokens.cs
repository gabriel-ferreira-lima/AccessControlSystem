using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using Microsoft.IdentityModel.Tokens;

namespace AccessControlSystem.Tests.Api.Support;

/// <summary>
/// Gera JWT assinados com a mesma chave da <see cref="ApiFactory"/>, no formato do <c>TokenService</c>
/// (<c>Id</c>, nome, perfil e, quando informada, a claim <c>tenant</c>), mas com controle total de cada
/// claim: ausente, malformada ou de um tenant diferente do operador.
/// </summary>
public static class TestTokens
{
    /// <summary>Token do operador no tenant informado (o caso normal).</summary>
    /// <remarks>
    /// Leva a versão de segurança que o objeto <paramref name="operator"/> tem NA MEMÓRIA. Depois de trocar senha,
    /// perfil ou situação pelo banco, o objeto antigo continua com a versão antiga: é exatamente um "token antigo".
    /// </remarks>
    public static string For(Operator @operator, Guid tenantId) =>
        Create(@operator.Id.ToString(), @operator.Email, @operator.Role, tenantId.ToString(),
            securityVersion: Version(@operator));

    /// <summary>Token no formato antigo: sem a claim <c>tenant</c> (mas com emissor, destinatário e versão válidos).</summary>
    public static string WithoutTenant(Operator @operator) =>
        Create(@operator.Id.ToString(), @operator.Email, @operator.Role, tenant: null,
            securityVersion: Version(@operator));

    /// <summary>Token com a claim <c>tenant</c> em texto arbitrário (ex.: malformada), com o resto válido.</summary>
    public static string WithRawTenant(Operator @operator, string tenant) =>
        Create(@operator.Id.ToString(), @operator.Email, @operator.Role, tenant,
            securityVersion: Version(@operator));

    private static string Version(Operator @operator) =>
        @operator.SecurityVersion.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Monta um token com controle total. <paramref name="issuer"/> e <paramref name="audience"/> têm como padrão o
    /// emissor e o destinatário esperados pela Api (<see cref="ApiFactory"/>); <c>null</c> omite a claim e um texto
    /// qualquer cria o caso errado. <paramref name="expires"/> permite um token vencido. <paramref name="algorithm"/>
    /// troca o algoritmo de assinatura (padrão HS256) e <c>null</c> gera um token sem assinatura.
    /// <paramref name="securityVersion"/> é o valor da claim <c>security_version</c> (padrão <c>"0"</c>, a versão de um
    /// operador novo); <c>null</c> omite a claim e um texto qualquer (ex.: <c>"abc"</c>) cria o caso malformado.
    /// </summary>
    public static string Create(
        string? id,
        string email,
        ERole role,
        string? tenant,
        string? issuer = ApiFactory.JwtIssuer,
        string? audience = ApiFactory.JwtAudience,
        DateTime? expires = null,
        string? algorithm = SecurityAlgorithms.HmacSha256Signature,
        string? securityVersion = "0")
    {
        var identity = new ClaimsIdentity();
        if (id is not null) identity.AddClaim(new Claim("Id", id));
        identity.AddClaim(new Claim(ClaimTypes.Name, email));
        identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
        if (tenant is not null) identity.AddClaim(new Claim("tenant", tenant));
        if (securityVersion is not null) identity.AddClaim(new Claim("security_version", securityVersion));

        var expiresAt = expires ?? DateTime.UtcNow.AddHours(1);

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = issuer,
            Audience = audience,
            // Um token vencido precisa de NotBefore anterior ao vencimento para o handler aceitar criá-lo.
            NotBefore = expiresAt.AddHours(-2),
            Expires = expiresAt,
            SigningCredentials = algorithm is null
                ? null
                : new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtKey)), algorithm)
        });
        return handler.WriteToken(token);
    }

    public static HttpRequestMessage Authorized(HttpMethod method, string uri, string token, HttpContent? content = null)
    {
        var request = new HttpRequestMessage(method, uri) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }
}
