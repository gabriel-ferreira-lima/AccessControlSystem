using AccessControlSystem.Api.Options;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AccessControlSystem.Api.Services.AccountContext {
    public class TokenService : ITokenService {

        private readonly SecretsOptions _secrets;
        private readonly JwtOptions _jwt;
        private readonly ITenantContext _tenantContext;

        public TokenService(IOptions<SecretsOptions> secrets, IOptions<JwtOptions> jwt, ITenantContext tenantContext) {
            _secrets = secrets.Value;
            _jwt = jwt.Value;
            _tenantContext = tenantContext;
        }

        public string Generate(Operator @operator) {
            // O token nasce já preso a um tenant. Sem tenant resolvido não há token.
            if (!_tenantContext.IsResolved) {
                throw new InvalidOperationException("Não é possível gerar um token sem o tenant resolvido.");
            }

            var handler = new JwtSecurityTokenHandler();
            var key = JwtSigningKey.GetBytes(_secrets.JwtPrivateKey);
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor {
                Subject = GenerateClaims(@operator, _tenantContext.TenantId),
                // Emissor e destinatário: a Api só aceita um token com estes dois valores (FR-010, FR-013).
                Issuer = _jwt.Issuer,
                Audience = _jwt.Audience,
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = credentials,
            };
            var token = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }

        private static ClaimsIdentity GenerateClaims(Operator @operator, Guid tenantId) {
            var ci = new ClaimsIdentity();

            ci.AddClaim(new Claim("Id", @operator.Id.ToString()));
            // O e-mail na claim Name é SÓ INFORMATIVO: trocar o e-mail não derruba o token, então ele pode ficar
            // desatualizado. Nenhuma decisão (identidade, acesso, limite) pode se basear nele; a identidade é o Id.
            ci.AddClaim(new Claim(ClaimTypes.Name, @operator.Email));
            ci.AddClaim(new Claim(ClaimTypes.Role, @operator.Role.ToString()));
            ci.AddClaim(new Claim("tenant", tenantId.ToString()));
            // Versão de segurança do operador no momento do login: o token só vale enquanto ela for a atual.
            ci.AddClaim(new Claim("security_version", @operator.SecurityVersion.ToString(CultureInfo.InvariantCulture)));

            return ci;
        }
    }
}
