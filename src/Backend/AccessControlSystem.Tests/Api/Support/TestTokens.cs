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
    public static string For(Operator @operator, Guid tenantId) =>
        Create(@operator.Id.ToString(), @operator.Email, @operator.Role, tenantId.ToString());

    /// <summary>Token no formato antigo: sem a claim <c>tenant</c>.</summary>
    public static string WithoutTenant(Operator @operator) =>
        Create(@operator.Id.ToString(), @operator.Email, @operator.Role, tenant: null);

    /// <summary>Token com a claim <c>tenant</c> em texto arbitrário (ex.: malformada).</summary>
    public static string WithRawTenant(Operator @operator, string tenant) =>
        Create(@operator.Id.ToString(), @operator.Email, @operator.Role, tenant);

    public static string Create(string? id, string email, ERole role, string? tenant)
    {
        var identity = new ClaimsIdentity();
        if (id is not null) identity.AddClaim(new Claim("Id", id));
        identity.AddClaim(new Claim(ClaimTypes.Name, email));
        identity.AddClaim(new Claim(ClaimTypes.Role, role.ToString()));
        if (tenant is not null) identity.AddClaim(new Claim("tenant", tenant));

        var handler = new JwtSecurityTokenHandler();
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = identity,
            Expires = DateTime.UtcNow.AddHours(1),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiFactory.JwtKey)),
                SecurityAlgorithms.HmacSha256Signature)
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
