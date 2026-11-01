using System.Text.Json;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Api;

/// <summary>
/// SC-003 / FR-037: o contrato dos 8 endpoints de <c>v1/account/*</c> não muda com o multi-tenant.
/// Os valores esperados vêm do código ATUAL dos handlers, de <c>Response</c>/<c>ResponseData</c> e dos
/// comentários XML do controller (nada disso é alterado por esta feature): qualquer diferença é
/// regressão de contrato, não motivo para ajustar o teste.
/// </summary>
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class ApiContractCompatTests : ApiTestBase
{
    // Nomes das propriedades JSON, em ordem alfabética (o envelope, e o que cada resposta traz em "data").
    private static readonly string[] SuccessEnvelope = ["data", "isSuccess", "message", "notifications", "status"];
    private static readonly string[] MiddlewareEnvelope = ["isSuccess", "message", "notifications", "status"];
    private static readonly string[] OperatorData = ["email", "id", "role"];
    private static readonly string[] OperatorStateData = ["email", "id", "isActive", "role"];

    public ApiContractCompatTests(PostgresContainerFixture database) : base(database)
    {
    }

    private sealed class Scenario
    {
        public Guid TenantId { get; init; }
        public Operator Admin { get; init; } = null!;
        public Operator Operator { get; init; } = null!;
        public Operator Inactive { get; init; } = null!;
        public string AdminToken => TestTokens.For(Admin, TenantId);
        public string OperatorToken => TestTokens.For(Operator, TenantId);
    }

    private async Task<Scenario> NewScenarioAsync()
    {
        var tenantId = await ApiFactory.RegisterTenantAsync(Database);
        var scenario = new Scenario
        {
            TenantId = tenantId,
            Admin = await Database.SeedOperatorAsync(tenantId, role: ERole.Admin),
            Operator = await Database.SeedOperatorAsync(tenantId, role: ERole.Operator),
            Inactive = await Database.SeedOperatorAsync(tenantId, role: ERole.Operator, isActive: false)
        };
        Factory.WithDefaultTenant(tenantId);
        return scenario;
    }

    private static void AssertEnvelope(Envelope envelope, int status, string message, string[]? properties = null)
    {
        Assert.Equal(status, envelope.HttpStatus);
        Assert.Equal(status, envelope.Status);
        Assert.Equal(message, envelope.Message);
        Assert.Equal(properties ?? SuccessEnvelope, envelope.PropertyNames);
    }

    private static void AssertSuccess(Envelope envelope, int status, string message, string[] dataProperties)
    {
        AssertEnvelope(envelope, status, message);
        Assert.Equal(dataProperties, envelope.DataPropertyNames);
    }

    private static void AssertHasNotifications(Envelope envelope)
    {
        Assert.Equal(JsonValueKind.Array, envelope.Root.GetProperty("notifications").ValueKind);
        Assert.NotEmpty(envelope.Root.GetProperty("notifications").EnumerateArray());
    }

    private string NewEmail() => TenantSeed.UniqueEmail("contrato");

    /// <summary>Um caso por situação documentada de cada endpoint, pelo nome (xUnit enumera cada um).</summary>
    private Dictionary<string, Func<Scenario, Task>> Cases => new()
    {
        // ---- POST v1/account/login (anônimo)
        ["login: 200 token e dados do operador"] = async s =>
        {
            var e = await ReadAsync(await LoginAsync(s.Admin.Email, OperatorPassword));
            AssertSuccess(e, 200, "Usuário autenticado com sucesso", ["email", "id", "role", "token"]);
        },
        ["login: 401 credenciais inválidas"] = async s =>
        {
            var e = await ReadAsync(await LoginAsync(s.Admin.Email, "Outra!Senha1"));
            AssertEnvelope(e, 401, "Credenciais inválidas");
        },
        ["login: 401 conta desativada"] = async s =>
        {
            var e = await ReadAsync(await LoginAsync(s.Inactive.Email, OperatorPassword));
            AssertEnvelope(e, 401, "Credenciais inválidas");
        },
        ["login: 400 e-mail em formato inválido"] = async s =>
        {
            var e = await ReadAsync(await LoginAsync("isto-nao-e-email", OperatorPassword));
            AssertEnvelope(e, 400, "Requisição inválida");
            AssertHasNotifications(e);
        },

        // ---- POST v1/account/create (admin)
        ["create: 201 conta criada"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Post, "v1/account/create", s.AdminToken,
                new { email = NewEmail(), password = OperatorPassword, role = 2 }));
            AssertSuccess(e, 201, "Operador criado com sucesso", OperatorData);
            Assert.Equal("Operator", e.Data.GetProperty("role").GetString());
        },
        ["create: 409 e-mail já em uso"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Post, "v1/account/create", s.AdminToken,
                new { email = s.Operator.Email.ToString(), password = OperatorPassword, role = 2 }));
            AssertEnvelope(e, 409, "Email já está em uso");
        },
        ["create: 400 requisição inválida"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Post, "v1/account/create", s.AdminToken,
                new { email = NewEmail(), password = "fraca", role = 2 }));
            AssertEnvelope(e, 400, "Requisição inválida");
            AssertHasNotifications(e);
        },

        // ---- GET v1/account/get-me (autenticado)
        ["get-me: 200 dados do próprio operador"] = async s =>
        {
            var e = await ReadAsync(await GetAsync("v1/account/get-me", s.OperatorToken));
            AssertSuccess(e, 200, "Operador retornado com sucesso", OperatorData);
            Assert.Equal(s.Operator.Id, e.Data.GetProperty("id").GetGuid());
        },

        // ---- GET v1/account/get-list (admin)
        ["get-list: 200 página de operadores"] = async s =>
        {
            var e = await ReadAsync(await GetAsync("v1/account/get-list", s.AdminToken));
            AssertSuccess(e, 200, "Lista retornada com sucesso", ["operators", "page", "size", "total", "totalPages"]);
            Assert.Equal(3, e.Data.GetProperty("total").GetInt32());   // só os 3 operadores deste tenant
            var first = e.Data.GetProperty("operators").EnumerateArray().First();
            Assert.Equal(OperatorStateData, first.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal));
        },
        ["get-list: 400 tamanho de página fora do intervalo"] = async s =>
        {
            var e = await ReadAsync(await GetAsync("v1/account/get-list?size=0", s.AdminToken));
            AssertEnvelope(e, 400, "Requisição inválida");
            AssertHasNotifications(e);
        },

        // ---- PUT v1/account/update-me (autenticado)
        ["update-me: 200 conta atualizada"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, "v1/account/update-me", s.OperatorToken,
                new { email = NewEmail() }));
            AssertSuccess(e, 200, "Usuario atualizado com sucesso", ["email", "id"]);
        },
        ["update-me: 400 nenhum campo informado"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, "v1/account/update-me", s.OperatorToken, new { }));
            // A mensagem tem um erro de digitação desde sempre ("Requsição"): é o contrato de hoje.
            AssertEnvelope(e, 400, "Requsição inválida");
        },
        ["update-me: 409 e-mail já em uso"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, "v1/account/update-me", s.OperatorToken,
                new { email = s.Admin.Email.ToString() }));
            AssertEnvelope(e, 409, "Email já está em uso");
        },

        // ---- PUT v1/account/update/{id} (admin)
        ["update: 200 conta atualizada"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, $"v1/account/update/{s.Operator.Id}", s.AdminToken,
                new { email = NewEmail() }));
            AssertSuccess(e, 200, "Conta atualizada com sucesso", OperatorData);
        },
        ["update: 404 operador não encontrado"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, $"v1/account/update/{Guid.NewGuid()}", s.AdminToken,
                new { email = NewEmail() }));
            AssertEnvelope(e, 404, "Conta não encontrada");
        },
        ["update: 409 e-mail já em uso"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, $"v1/account/update/{s.Operator.Id}", s.AdminToken,
                new { email = s.Admin.Email.ToString() }));
            AssertEnvelope(e, 409, "Email já está em uso");
        },

        // ---- PUT v1/account/activate/{id} (admin)
        ["activate: 200 conta reativada"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, $"v1/account/activate/{s.Inactive.Id}", s.AdminToken));
            AssertSuccess(e, 200, "Operador reativado com sucesso", OperatorStateData);
            Assert.True(e.Data.GetProperty("isActive").GetBoolean());
        },
        ["activate: 404 operador não encontrado"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, $"v1/account/activate/{Guid.NewGuid()}", s.AdminToken));
            AssertEnvelope(e, 404, "Conta não encontrada");
        },

        // ---- PUT v1/account/deactivate/{id} (admin)
        ["deactivate: 200 conta desativada"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, $"v1/account/deactivate/{s.Operator.Id}", s.AdminToken));
            AssertSuccess(e, 200, "Operador desativado com sucesso", OperatorStateData);
            Assert.False(e.Data.GetProperty("isActive").GetBoolean());
        },
        ["deactivate: 404 operador não encontrado"] = async s =>
        {
            var e = await ReadAsync(await SendAsync(HttpMethod.Put, $"v1/account/deactivate/{Guid.NewGuid()}", s.AdminToken));
            AssertEnvelope(e, 404, "Conta não encontrada");
        },

        // ---- Respostas do pipeline de autenticação (valem para todos os endpoints protegidos)
        ["pipeline: 401 sem token"] = async _ =>
        {
            var e = await ReadAsync(await GetAsync("v1/account/get-me"));
            AssertEnvelope(e, 401, "Não autenticado", MiddlewareEnvelope);
        },
        ["pipeline: 403 operador em endpoint de admin"] = async s =>
        {
            var e = await ReadAsync(await GetAsync("v1/account/get-list", s.OperatorToken));
            AssertEnvelope(e, 403, "Sem permissão", MiddlewareEnvelope);
        },
    };

    // O dicionário de casos depende da instância (usa os helpers dela); a teoria enumera os nomes daqui.
    public static IEnumerable<object[]> CaseNames => CaseNameList.Select(name => new object[] { name });

    private static readonly string[] CaseNameList =
    [
        "login: 200 token e dados do operador",
        "login: 401 credenciais inválidas",
        "login: 401 conta desativada",
        "login: 400 e-mail em formato inválido",
        "create: 201 conta criada",
        "create: 409 e-mail já em uso",
        "create: 400 requisição inválida",
        "get-me: 200 dados do próprio operador",
        "get-list: 200 página de operadores",
        "get-list: 400 tamanho de página fora do intervalo",
        "update-me: 200 conta atualizada",
        "update-me: 400 nenhum campo informado",
        "update-me: 409 e-mail já em uso",
        "update: 200 conta atualizada",
        "update: 404 operador não encontrado",
        "update: 409 e-mail já em uso",
        "activate: 200 conta reativada",
        "activate: 404 operador não encontrado",
        "deactivate: 200 conta desativada",
        "deactivate: 404 operador não encontrado",
        "pipeline: 401 sem token",
        "pipeline: 403 operador em endpoint de admin",
    ];

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task Contrato_dos_8_endpoints_nao_mudou(string caseName)
    {
        var scenario = await NewScenarioAsync();

        await Cases[caseName](scenario);
    }

    [Fact]
    public void Todos_os_casos_do_contrato_estao_na_teoria()
    {
        // Garante que a lista de nomes da teoria e o dicionário de casos não se separem.
        Assert.Equal(CaseNameList.OrderBy(n => n), Cases.Keys.OrderBy(n => n));
    }

    [Fact]
    public void O_contrato_cobre_os_8_endpoints_de_account()
    {
        var covered = CaseNameList.Select(n => n.Split(':')[0]).ToHashSet();

        Assert.Superset(
            new HashSet<string> { "login", "create", "get-me", "get-list", "update-me", "update", "activate", "deactivate" },
            covered);
    }
}
