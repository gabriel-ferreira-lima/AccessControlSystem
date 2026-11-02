using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AccessControlSystem.Api.Options;
using AccessControlSystem.Api.Services.AccountContext;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AccessControlSystem.Tests.Api;

public class TokenServiceTests
{
    private const string TestKey = "chave-de-teste-para-hmac-sha256-com-mais-de-256-bits-aaaaaaaaaaaa";

    private const string TestIssuer = "emissor-de-teste";
    private const string TestAudience = "destinatario-de-teste";

    private static readonly Guid TenantId = Guid.Parse("b1b1b1b1-0000-4000-8000-000000000001");

    private readonly ITenantContext _tenant = Substitute.For<ITenantContext>();
    private readonly TokenService _service;

    public TokenServiceTests()
    {
        // A chave vem das opções (SecretsOptions) e o tenant do contexto da requisição.
        _tenant.IsResolved.Returns(true);
        _tenant.TenantId.Returns(TenantId);

        _service = new TokenService(
            Options.Create(new SecretsOptions { JwtPrivateKey = TestKey }),
            Options.Create(new JwtOptions { Issuer = TestIssuer, Audience = TestAudience }),
            _tenant);
    }

    private static Operator NewOperator(ERole role) =>
        new(new Email("op@test.com"), new Password("Str0ng!Pass"), role);

    private static ClaimsPrincipal Validate(string token) =>
        new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
        {
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        }, out _);

    [Fact]
    public void Gera_um_jwt_assinado_com_tres_partes()
    {
        var token = _service.Generate(NewOperator(ERole.Admin));

        Assert.Equal(3, token.Split('.').Length);
        Validate(token);   // não lança => assinatura confere com a chave
    }

    [Fact]
    public void Token_carrega_id_email_e_role_do_operador()
    {
        var op = NewOperator(ERole.Operator);

        var principal = Validate(_service.Generate(op));

        Assert.Equal(op.Id.ToString(), principal.FindFirstValue("Id"));
        Assert.Equal("op@test.com", principal.FindFirstValue(ClaimTypes.Name));
        Assert.Equal("Operator", principal.FindFirstValue(ClaimTypes.Role));
    }

    [Fact]
    public void Token_expira_em_cerca_de_oito_horas()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_service.Generate(NewOperator(ERole.Admin)));

        var horas = (jwt.ValidTo - DateTime.UtcNow).TotalHours;

        Assert.InRange(horas, 7.9, 8.1);
    }

    [Fact]
    public void Token_carrega_a_claim_tenant_do_contexto()
    {
        var principal = Validate(_service.Generate(NewOperator(ERole.Admin)));

        Assert.Equal(TenantId.ToString(), principal.FindFirstValue("tenant"));
    }

    [Fact]
    public void Token_carrega_emissor_e_destinatario_das_opcoes()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_service.Generate(NewOperator(ERole.Admin)));

        Assert.Equal(TestIssuer, jwt.Issuer);
        Assert.Equal([TestAudience], jwt.Audiences);
    }

    [Fact]
    public void Token_carrega_a_security_version_do_operador()
    {
        var op = NewOperator(ERole.Admin);
        op.UpdatePassword(new Password("Nova1!Senha"));   // sobe a versão de segurança para 1

        var principal = Validate(_service.Generate(op));

        Assert.Equal("1", principal.FindFirstValue("security_version"));
    }

    // Guarda: o algoritmo já é HS256 hoje; o teste impede que mudem a assinatura sem a Api (que fixa HS256) saber.
    [Fact]
    public void Token_e_assinado_com_hs256()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_service.Generate(NewOperator(ERole.Admin)));

        Assert.Equal(SecurityAlgorithms.HmacSha256, jwt.Header.Alg);
    }

    [Fact]
    public void Gerar_token_sem_tenant_resolvido_lanca()
    {
        _tenant.IsResolved.Returns(false);

        Assert.Throws<InvalidOperationException>(() => _service.Generate(NewOperator(ERole.Admin)));
    }
}
