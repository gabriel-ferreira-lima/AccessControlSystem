using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Api;

// FR-014 e FR-015: e-mail inexistente, conta inativa e senha errada devolvem a MESMA 401 "Credenciais inválidas", e a
// conta inativa nunca recebe token. O e-mail do login é normalizado (sem espaços nas pontas e em minúsculas), como a
// conta do limite de tentativas (decisão R-09).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class LoginUniformityApiTests : ApiTestBase
{
    public LoginUniformityApiTests(PostgresContainerFixture database) : base(database)
    {
    }

    [Fact]
    public async Task Login_com_email_em_maiusculas_funciona()
    {
        var (tenantId, op) = await TenantWithOperatorAsync(ERole.Operator);
        Factory.WithDefaultTenant(tenantId);

        var envelope = await ReadAsync(await LoginAsync(op.Email.Address.ToUpperInvariant(), OperatorPassword));

        Assert.Equal(200, envelope.HttpStatus);
        Assert.False(string.IsNullOrWhiteSpace(envelope.Data.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task Login_com_email_com_espacos_nas_pontas_funciona()
    {
        var (tenantId, op) = await TenantWithOperatorAsync(ERole.Operator);
        Factory.WithDefaultTenant(tenantId);

        var envelope = await ReadAsync(await LoginAsync($"  {op.Email.Address} ", OperatorPassword));

        Assert.Equal(200, envelope.HttpStatus);
    }

    // Guarda: já passa hoje.
    [Fact]
    public async Task Falhas_do_login_tem_corpo_identico_nos_tres_casos()
    {
        var tenantId = await ApiFactory.RegisterTenantAsync(Database);
        var ativo = await Database.SeedOperatorAsync(tenantId);
        var inativo = await Database.SeedOperatorAsync(tenantId, isActive: false);
        Factory.WithDefaultTenant(tenantId);

        var inexistente = await ReadAsync(await LoginAsync(TenantSeed.UniqueEmail("ninguem"), OperatorPassword));
        var contaInativa = await ReadAsync(await LoginAsync(inativo.Email, OperatorPassword));   // senha correta
        var senhaErrada = await ReadAsync(await LoginAsync(ativo.Email, "Errada1!"));

        foreach (var falha in new[] { inexistente, contaInativa, senhaErrada })
        {
            Assert.Equal(401, falha.HttpStatus);
            Assert.Equal(401, falha.Status);
            Assert.Equal("Credenciais inválidas", falha.Message);
            Assert.False(falha.HasData);
        }

        // Corpo idêntico, byte a byte.
        Assert.Equal(inexistente.Body, contaInativa.Body);
        Assert.Equal(inexistente.Body, senhaErrada.Body);
    }

    // Guarda: já passa hoje.
    [Fact]
    public async Task Conta_inativa_com_senha_correta_nao_recebe_token()
    {
        var tenantId = await ApiFactory.RegisterTenantAsync(Database);
        var inativo = await Database.SeedOperatorAsync(tenantId, isActive: false);
        Factory.WithDefaultTenant(tenantId);

        var envelope = await ReadAsync(await LoginAsync(inativo.Email, OperatorPassword));

        Assert.Equal(401, envelope.HttpStatus);
        Assert.DoesNotContain("token", envelope.Body, StringComparison.OrdinalIgnoreCase);
    }
}
