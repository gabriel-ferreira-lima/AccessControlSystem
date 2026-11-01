using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Api;

// O token só vale se o tenant dele existir, estiver ativo e tiver base configurada; e a resposta de
// recusa é sempre a mesma de hoje, sem revelar o motivo (FR-014 a FR-016, FR-037).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class JwtTenantValidationTests : ApiTestBase
{
    private const string GetMe = "v1/account/get-me";

    public JwtTenantValidationTests(PostgresContainerFixture database) : base(database)
    {
    }

    private async Task AssertGenericUnauthorizedAsync(HttpResponseMessage response)
    {
        var envelope = await ReadAsync(response);

        Assert.Equal(401, envelope.HttpStatus);
        Assert.Equal("Não autenticado", envelope.Message);
        Assert.Equal(401, envelope.Status);
    }

    [Fact]
    public async Task Token_sem_claim_tenant_retorna_401()
    {
        var (_, admin) = await TenantWithOperatorAsync();

        var response = await GetAsync(GetMe, TestTokens.WithoutTenant(admin));

        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Token_com_tenant_malformado_retorna_401()
    {
        var (_, admin) = await TenantWithOperatorAsync();

        var response = await GetAsync(GetMe, TestTokens.WithRawTenant(admin, "isto-nao-e-um-guid"));

        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Token_de_tenant_inexistente_retorna_401()
    {
        var (_, admin) = await TenantWithOperatorAsync();

        var response = await GetAsync(GetMe, TestTokens.For(admin, Guid.NewGuid()));

        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Token_de_tenant_inativo_retorna_401()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync(isActive: false);

        var response = await GetAsync(GetMe, TestTokens.For(admin, tenantId));

        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Tenant_inativado_depois_do_login_retorna_401_na_requisicao_seguinte()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(tenantId);

        var login = await ReadAsync(await LoginAsync(admin.Email, OperatorPassword));
        Assert.Equal(200, login.HttpStatus);
        var token = login.Data.GetProperty("token").GetString()!;

        Assert.Equal(200, (int)(await GetAsync(GetMe, token)).StatusCode);

        // Sem cache: a mudança no catálogo vale na requisição seguinte, com o mesmo token.
        await ApiFactory.SetTenantActiveAsync(Database, tenantId, isActive: false);

        await AssertGenericUnauthorizedAsync(await GetAsync(GetMe, token));
    }

    [Fact]
    public async Task Token_com_id_de_um_tenant_e_tenant_de_outro_retorna_401()
    {
        var (_, adminA) = await TenantWithOperatorAsync();
        var (tenantB, _) = await TenantWithOperatorAsync();

        // O operador existe no tenant A, mas a claim diz B: sob o RLS de B ele não existe.
        var response = await GetAsync(GetMe, TestTokens.For(adminA, tenantB));

        await AssertGenericUnauthorizedAsync(response);
    }

    [Fact]
    public async Task Token_antigo_sem_tenant_retorna_401()
    {
        // Token de antes desta feature: assinatura válida e operador existente, mas sem a claim tenant.
        var (_, admin) = await TenantWithOperatorAsync();
        var oldToken = TestTokens.Create(admin.Id.ToString(), admin.Email, admin.Role, tenant: null);

        await AssertGenericUnauthorizedAsync(await GetAsync(GetMe, oldToken));
    }

    [Fact]
    public async Task Mensagem_do_401_e_a_generica_de_hoje()
    {
        var (tenantOk, admin) = await TenantWithOperatorAsync();
        var (tenantInactive, inactiveAdmin) = await TenantWithOperatorAsync(isActive: false);
        var (otherTenant, _) = await TenantWithOperatorAsync();

        // A resposta sem token é a de hoje: é com ela que todas as outras recusas têm de coincidir.
        var baseline = await ReadAsync(await GetAsync(GetMe));
        Assert.Equal(401, baseline.HttpStatus);
        Assert.Equal("Não autenticado", baseline.Message);

        var tokens = new Dictionary<string, string>
        {
            ["sem tenant"] = TestTokens.WithoutTenant(admin),
            ["tenant malformado"] = TestTokens.WithRawTenant(admin, "xyz"),
            ["tenant inexistente"] = TestTokens.For(admin, Guid.NewGuid()),
            ["tenant inativo"] = TestTokens.For(inactiveAdmin, tenantInactive),
            ["id de um tenant e tenant de outro"] = TestTokens.For(admin, otherTenant),
            ["operador inexistente"] = TestTokens.For(TenantSeed.NewOperator(), tenantOk),
        };

        foreach (var (reason, token) in tokens)
        {
            var envelope = await ReadAsync(await GetAsync(GetMe, token));

            Assert.True(envelope.HttpStatus == 401, $"'{reason}' devia retornar 401, retornou {envelope.HttpStatus}.");
            Assert.True(
                envelope.Body == baseline.Body,
                $"'{reason}' devia ter o mesmo corpo da resposta sem token ({baseline.Body}), mas veio {envelope.Body}.");
        }
    }

    [Fact]
    public async Task Token_valido_le_get_me_com_os_dados_do_proprio_tenant()
    {
        // O mesmo e-mail nos dois tenants: cada token enxerga só o operador do seu tenant.
        var email = TenantSeed.UniqueEmail("mesmo");
        var tenantA = await ApiFactory.RegisterTenantAsync(Database);
        var tenantB = await ApiFactory.RegisterTenantAsync(Database);
        var operatorA = await Database.SeedOperatorAsync(tenantA, email, ERole.Admin);
        var operatorB = await Database.SeedOperatorAsync(tenantB, email, ERole.Admin);

        var meA = await ReadAsync(await GetAsync(GetMe, TestTokens.For(operatorA, tenantA)));
        var meB = await ReadAsync(await GetAsync(GetMe, TestTokens.For(operatorB, tenantB)));

        Assert.Equal(200, meA.HttpStatus);
        Assert.Equal(operatorA.Id, meA.Data.GetProperty("id").GetGuid());
        Assert.Equal(200, meB.HttpStatus);
        Assert.Equal(operatorB.Id, meB.Data.GetProperty("id").GetGuid());
        Assert.NotEqual(operatorA.Id, operatorB.Id);
    }

    [Fact]
    public async Task Qualquer_instancia_atende_qualquer_tenant()
    {
        // A Api tem o tenant A como padrão do login, mas atende o token do tenant B (base dedicada).
        var (tenantA, _) = await TenantWithOperatorAsync();
        var (tenantB, adminB) = await TenantWithOperatorAsync(dedicated: true);
        Factory.WithDefaultTenant(tenantA);

        var response = await ReadAsync(await GetAsync(GetMe, TestTokens.For(adminB, tenantB)));

        Assert.Equal(200, response.HttpStatus);
        Assert.Equal(adminB.Id, response.Data.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Base_do_tenant_sem_connection_string_retorna_500()
    {
        // Armadilha: o operador existe na base compartilhada com o id deste tenant. Se a Api caísse
        // em outra base, ela autenticaria; o certo é recusar (500), nunca "adivinhar" a base.
        var tenantId = await ApiFactory.RegisterTenantAsync(Database, databaseName: $"sem_config_{Guid.NewGuid():N}");
        var trap = await Database.SeedOperatorAsync(tenantId, role: ERole.Admin);

        var response = await ReadAsync(await GetAsync(GetMe, TestTokens.For(trap, tenantId)));

        Assert.Equal(500, response.HttpStatus);
        Assert.Equal("Erro interno do servidor", response.Message);
        Assert.DoesNotContain("Host=", response.Body);
        Assert.DoesNotContain("Password", response.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Base_do_tenant_inacessivel_retorna_500_e_nunca_cai_em_outra_base()
    {
        var databaseName = $"inacessivel_{Guid.NewGuid():N}";
        Factory.WithTenantDatabase(
            databaseName,
            "Host=127.0.0.1;Port=1;Database=nao_existe;Username=acs_app;Password=x;Timeout=2;Command Timeout=2");

        var tenantId = await ApiFactory.RegisterTenantAsync(Database, databaseName: databaseName);
        var trap = await Database.SeedOperatorAsync(tenantId, role: ERole.Admin);   // mesma armadilha da base compartilhada

        var response = await ReadAsync(await GetAsync(GetMe, TestTokens.For(trap, tenantId)));

        Assert.Equal(500, response.HttpStatus);
        Assert.Equal("Erro interno do servidor", response.Message);
        Assert.DoesNotContain("127.0.0.1", response.Body);

        // A falha de um tenant não contamina os outros.
        var (healthyTenant, healthyAdmin) = await TenantWithOperatorAsync();
        Assert.Equal(200, (int)(await GetAsync(GetMe, TestTokens.For(healthyAdmin, healthyTenant))).StatusCode);
    }

    [Theory]
    [InlineData("activate")]
    [InlineData("deactivate")]
    [InlineData("update")]
    public async Task Operador_de_outro_tenant_por_id_retorna_404_como_inexistente(string action)
    {
        var (tenantA, adminA) = await TenantWithOperatorAsync();
        var tenantB = await ApiFactory.RegisterTenantAsync(Database);
        var victim = await Database.SeedOperatorAsync(tenantB, role: ERole.Operator, isActive: action != "activate");
        var originalEmail = victim.Email.ToString();

        var response = await ReadAsync(action switch
        {
            "update" => await SendAsync(HttpMethod.Put, $"v1/account/update/{victim.Id}", TestTokens.For(adminA, tenantA), new { email = TenantSeed.UniqueEmail("invasor") }),
            _ => await SendAsync(HttpMethod.Put, $"v1/account/{action}/{victim.Id}", TestTokens.For(adminA, tenantA))
        });

        Assert.Equal(404, response.HttpStatus);
        Assert.Equal("Conta não encontrada", response.Message);

        // E o operador do outro tenant continua exatamente como estava.
        await using var context = Database.CreateContext(tenantB);
        var stored = await context.Operators.AsNoTracking().SingleAsync(x => x.Id == victim.Id);
        Assert.Equal(originalEmail, stored.Email.ToString());
        Assert.Equal(action != "activate", stored.IsActive);
    }
}
