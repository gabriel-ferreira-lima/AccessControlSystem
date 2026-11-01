using AccessControlSystem.Api.Options;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AccessControlSystem.Api.Services.AccountContext {
    public class TokenService : ITokenService {

        private readonly SecretsOptions _secrets;
        private readonly ITenantContext _tenantContext;

        public TokenService(IOptions<SecretsOptions> secrets, ITenantContext tenantContext) {
            _secrets = secrets.Value;
            _tenantContext = tenantContext;
        }

        public string Generate(Operator @operator) {
            // O token nasce já preso a um tenant. Sem tenant resolvido não há token.
            if (!_tenantContext.IsResolved) {
                throw new InvalidOperationException("Não é possível gerar um token sem o tenant resolvido.");
            }

            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_secrets.JwtPrivateKey);
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor {
                Subject = GenerateClaims(@operator, _tenantContext.TenantId),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = credentials,
            };
            var token = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }

        private static ClaimsIdentity GenerateClaims(Operator @operator, Guid tenantId) {
            var ci = new ClaimsIdentity();

            ci.AddClaim(new Claim("Id", @operator.Id.ToString()));
            ci.AddClaim(new Claim(ClaimTypes.Name, @operator.Email));
            ci.AddClaim(new Claim(ClaimTypes.Role, @operator.Role.ToString()));
            ci.AddClaim(new Claim("tenant", tenantId.ToString()));

            return ci;
        }
    }
}
