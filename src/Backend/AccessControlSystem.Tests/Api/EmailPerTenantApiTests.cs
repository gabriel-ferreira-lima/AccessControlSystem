using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Api;

// FR-019: o e-mail é único dentro do tenant. O mesmo e-mail pode existir em tenants diferentes e o
// 409 de hoje continua valendo dentro do tenant.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class EmailPerTenantApiTests : ApiTestBase
{
    public EmailPerTenantApiTests(PostgresContainerFixture database) : base(database)
    {
    }

    private Task<HttpResponseMessage> CreateAsync(string token, string email) =>
        SendAsync(HttpMethod.Post, "v1/account/create", token,
            new { email, password = OperatorPassword, role = 2 });

    [Fact]
    public async Task Cadastro_de_email_de_outro_tenant_retorna_201()
    {
        var (tenantA, adminA) = await TenantWithOperatorAsync();
        var (tenantB, adminB) = await TenantWithOperatorAsync(dedicated: true);
        var email = TenantSeed.UniqueEmail("compartilhado");

        var emA = await ReadAsync(await CreateAsync(TestTokens.For(adminA, tenantA), email));
        var emB = await ReadAsync(await CreateAsync(TestTokens.For(adminB, tenantB), email));

        Assert.Equal(201, emA.HttpStatus);
        Assert.Equal(201, emB.HttpStatus);
        Assert.Equal("Operador criado com sucesso", emB.Message);
        Assert.NotEqual(emA.Data.GetProperty("id").GetGuid(), emB.Data.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Cadastro_de_email_do_mesmo_tenant_retorna_409_com_a_mensagem_de_hoje()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();
        var token = TestTokens.For(admin, tenantId);
        var email = TenantSeed.UniqueEmail("repetido");

        Assert.Equal(201, (int)(await CreateAsync(token, email)).StatusCode);
        var segundo = await ReadAsync(await CreateAsync(token, email));

        Assert.Equal(409, segundo.HttpStatus);
        Assert.Equal("Email já está em uso", segundo.Message);
    }

    [Fact]
    public async Task Alterar_email_ou_situacao_em_um_tenant_nao_afeta_o_outro()
    {
        // Mesmo e-mail nos dois tenants, na mesma base compartilhada.
        var email = TenantSeed.UniqueEmail("gemeo");
        var tenantA = await ApiFactory.RegisterTenantAsync(Database);
        var tenantB = await ApiFactory.RegisterTenantAsync(Database);
        var adminA = await Database.SeedOperatorAsync(tenantA, role: ERole.Admin);
        var gemeoA = await Database.SeedOperatorAsync(tenantA, email);
        var gemeoB = await Database.SeedOperatorAsync(tenantB, email);
        var token = TestTokens.For(adminA, tenantA);
        var novoEmail = TenantSeed.UniqueEmail("novo");

        var alterar = await SendAsync(HttpMethod.Put, $"v1/account/update/{gemeoA.Id}", token, new { email = novoEmail });
        var desativar = await SendAsync(HttpMethod.Put, $"v1/account/deactivate/{gemeoA.Id}", token);

        Assert.Equal(200, (int)alterar.StatusCode);
        Assert.Equal(200, (int)desativar.StatusCode);

        // O gêmeo do tenant B continua com o e-mail e a situação de antes.
        await using var contextB = Database.CreateContext(tenantB);
        var depois = await contextB.Operators.AsNoTracking().SingleAsync(x => x.Id == gemeoB.Id);
        Assert.Equal(email, depois.Email.Address);
        Assert.True(depois.IsActive);

        // E o do tenant A mudou de fato.
        await using var contextA = Database.CreateContext(tenantA);
        var alterado = await contextA.Operators.AsNoTracking().SingleAsync(x => x.Id == gemeoA.Id);
        Assert.Equal(novoEmail, alterado.Email.Address);
        Assert.False(alterado.IsActive);
    }
}
