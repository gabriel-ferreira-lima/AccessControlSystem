using AccessControlSystem.Application;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AccessControlSystem.Api.Services.AccountContext {
    public class TokenService : ITokenService {
        public string Generate(Operator @operator) {
            var handler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(Configuration.Secrets.JwtPrivateKey);
            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor {
                Subject = GenerateClaims(@operator),
                Expires = DateTime.UtcNow.AddHours(8),
                SigningCredentials = credentials,
            };
            var token = handler.CreateToken(tokenDescriptor);
            return handler.WriteToken(token);
        }

        private static ClaimsIdentity GenerateClaims(Operator @operator) {
            var ci = new ClaimsIdentity();

            ci.AddClaim(new Claim("Id", @operator.Id.ToString()));
            ci.AddClaim(new Claim(ClaimTypes.Name, @operator.Email));
            ci.AddClaim(new Claim(ClaimTypes.Role, @operator.Role.ToString()));

            return ci;
        }
    }
}
