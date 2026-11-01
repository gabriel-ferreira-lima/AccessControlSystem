using System.IdentityModel.Tokens.Jwt;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;
using Microsoft.Extensions.Logging;

namespace AccessControlSystem.Tests.Api;

// Enquanto não existe o login com escolha de tenant, o login usa o tenant padrão da configuração
// (Login:DefaultTenantId). O contrato de resposta é o de hoje (FR-017, FR-018, FR-037).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class DefaultLoginTenantTests : ApiTestBase
{
    private const string CredenciaisInvalidas = "Credenciais inválidas";

    public DefaultLoginTenantTests(PostgresContainerFixture database) : base(database)
    {
    }

    private async Task AssertCredenciaisInvalidasAsync(HttpResponseMessage response)
    {
        var envelope = await ReadAsync(response);

        Assert.Equal(401, envelope.HttpStatus);
        Assert.Equal(CredenciaisInvalidas, envelope.Message);
        Assert.Equal(401, envelope.Status);
    }

    [Fact]
    public async Task Login_usa_o_tenant_padrao_e_o_token_carrega_a_claim_tenant()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(tenantId);

        var envelope = await ReadAsync(await LoginAsync(admin.Email, OperatorPassword));

        Assert.Equal(200, envelope.HttpStatus);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(envelope.Data.GetProperty("token").GetString());
        Assert.Equal(tenantId.ToString(), token.Claims.Single(c => c.Type == "tenant").Value);
        Assert.Equal(admin.Id.ToString(), token.Claims.Single(c => c.Type == "Id").Value);
    }

    [Fact]
    public async Task Login_mantem_campos_status_e_mensagem_de_hoje()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync(ERole.Operator);
        Factory.WithDefaultTenant(tenantId);

        var envelope = await ReadAsync(await LoginAsync(admin.Email, OperatorPassword));

        Assert.Equal(200, envelope.HttpStatus);
        Assert.Equal("Usuário autenticado com sucesso", envelope.Message);
        Assert.Equal(200, envelope.Status);
        Assert.Equal(new[] { "data", "isSuccess", "message", "notifications", "status" }, envelope.PropertyNames);
        Assert.Equal(new[] { "email", "id", "role", "token" }, envelope.DataPropertyNames);
        Assert.Equal(admin.Id, envelope.Data.GetProperty("id").GetGuid());
        Assert.Equal(admin.Email.ToString(), envelope.Data.GetProperty("email").GetString());
        Assert.Equal("Operator", envelope.Data.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Login_com_tenant_padrao_ausente_retorna_401_credenciais_invalidas()
    {
        var (_, admin) = await TenantWithOperatorAsync();
        // Nenhum tenant padrão configurado.

        await AssertCredenciaisInvalidasAsync(await LoginAsync(admin.Email, OperatorPassword));
    }

    [Fact]
    public async Task Login_com_tenant_padrao_inexistente_retorna_401()
    {
        var (_, admin) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(Guid.NewGuid());

        await AssertCredenciaisInvalidasAsync(await LoginAsync(admin.Email, OperatorPassword));
    }

    [Fact]
    public async Task Login_com_tenant_padrao_inativo_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync(isActive: false);
        Factory.WithDefaultTenant(tenantId);

        await AssertCredenciaisInvalidasAsync(await LoginAsync(admin.Email, OperatorPassword));
    }

    [Theory]
    [InlineData("lixo")]
    [InlineData("de outro tenant")]
    [InlineData("de tenant inativo")]
    public async Task Token_enviado_ao_login_e_ignorado(string kind)
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(tenantId);

        var (otherTenant, otherAdmin) = await TenantWithOperatorAsync();
        var (inactiveTenant, inactiveAdmin) = await TenantWithOperatorAsync(isActive: false);
        var token = kind switch
        {
            "de outro tenant" => TestTokens.For(otherAdmin, otherTenant),
            "de tenant inativo" => TestTokens.For(inactiveAdmin, inactiveTenant),
            _ => "isto.nao.e-um-jwt"
        };

        // O login é anônimo: um token qualquer (válido, de outro tenant, inválido) não muda nada.
        var envelope = await ReadAsync(await LoginAsync(admin.Email, OperatorPassword, bearerToken: token));

        Assert.Equal(200, envelope.HttpStatus);
        Assert.Equal(admin.Id, envelope.Data.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Login_nao_revela_operadores_de_outros_tenants()
    {
        var (tenantA, _) = await TenantWithOperatorAsync();
        var (_, adminB) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(tenantA);

        var doOutroTenant = await ReadAsync(await LoginAsync(adminB.Email, OperatorPassword));
        var inexistente = await ReadAsync(await LoginAsync(TenantSeed.UniqueEmail("ninguem"), OperatorPassword));

        // Existir em outro tenant é indistinguível de não existir.
        Assert.Equal(401, doOutroTenant.HttpStatus);
        Assert.Equal(CredenciaisInvalidas, doOutroTenant.Message);
        Assert.Equal(inexistente.Body, doOutroTenant.Body);
    }

    [Fact]
    public async Task Motivo_do_401_vai_para_o_log_sem_tenant_token_email_ou_segredo()
    {
        var email = TenantSeed.UniqueEmail("sigilo");
        var tenantId = Guid.NewGuid();
        Factory.WithDefaultTenant(tenantId);   // inexistente no catálogo
        var token = TestTokens.Create(Guid.NewGuid().ToString(), email, ERole.Admin, tenant: null);

        await AssertCredenciaisInvalidasAsync(await LoginAsync(email, "Senha-Secreta-123!", bearerToken: token));

        // O motivo precisa estar no log (a operação o procura lá)...
        Assert.Contains(Factory.Logs, l => l.Level >= LogLevel.Warning && l.Text.Contains("Login:DefaultTenantId"));

        // ...mas sem nada que identifique tenant, operador, token ou segredo.
        foreach (var log in Factory.Logs)
        {
            Assert.DoesNotContain(tenantId.ToString(), log.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(email, log.Text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(token, log.Text);
            Assert.DoesNotContain("Senha-Secreta-123!", log.Text);
            Assert.DoesNotContain(ApiFactory.JwtKey, log.Text);
        }
    }
}
