using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Api;

// FR-001 a FR-006: os 4 endpoints que recebem texto recusam campo acima do limite com 400 "Requisição inválida" e a
// notificação do campo; no limite exato a validação de tamanho não barra. Contrato: contracts/input-limits.md.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class InputLimitsApiTests : ApiTestBase
{
    private const string RequisicaoInvalida = "Requisição inválida";

    public InputLimitsApiTests(PostgresContainerFixture database) : base(database)
    {
    }

    // O Email.Pattern não limita a parte local: prefixo único (Guid) + 'a' + "@test.com" somando exatamente o tamanho pedido.
    private static string EmailComTamanho(int tamanho)
    {
        var parteLocal = Guid.NewGuid().ToString("N") + new string('a', tamanho - "@test.com".Length - 32);
        return parteLocal + "@test.com";
    }

    // Atende ao Password.Pattern (maiúscula, minúscula, dígito e símbolo) com exatamente o tamanho pedido.
    private static string SenhaForteComTamanho(int tamanho) => "Aa1!" + new string('x', tamanho - 4);

    private static string[] KeysOf(Envelope envelope) =>
        envelope.Root.GetProperty("notifications").EnumerateArray()
            .Select(n => n.GetProperty("key").GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void AssertRequisicaoInvalida(Envelope envelope, params string[] keys)
    {
        Assert.Equal(400, envelope.HttpStatus);
        Assert.Equal(400, envelope.Status);
        Assert.Equal(RequisicaoInvalida, envelope.Message);
        Assert.Equal(keys.Order(StringComparer.Ordinal).ToArray(), KeysOf(envelope));
    }

    #region Login

    [Fact]
    public async Task Login_com_senha_acima_do_limite_devolve_400_com_a_notificacao_da_senha()
    {
        var (tenantId, op) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(tenantId);

        var envelope = await ReadAsync(await LoginAsync(op.Email.Address, new string('a', 129)));

        AssertRequisicaoInvalida(envelope, "Password");
    }

    [Fact]
    public async Task Login_com_email_acima_do_limite_devolve_400_com_a_notificacao_do_email()
    {
        var (tenantId, _) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(tenantId);

        var envelope = await ReadAsync(await LoginAsync(EmailComTamanho(256), OperatorPassword));

        AssertRequisicaoInvalida(envelope, "Email");
    }

    [Fact]
    public async Task Login_com_senha_de_exatamente_128_caracteres_nao_e_barrado_pelo_tamanho()
    {
        var (tenantId, op) = await TenantWithOperatorAsync();
        Factory.WithDefaultTenant(tenantId);

        var envelope = await ReadAsync(await LoginAsync(op.Email.Address, new string('x', 128)));

        Assert.Equal(401, envelope.HttpStatus);   // credencial errada, não "Requisição inválida"
        Assert.Equal("Credenciais inválidas", envelope.Message);
    }

    #endregion

    #region Create

    [Fact]
    public async Task Create_com_email_e_senha_acima_do_limite_devolve_400_com_uma_notificacao_por_campo()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync(ERole.Admin);

        var envelope = await ReadAsync(await SendAsync(
            HttpMethod.Post, "v1/account/create", TestTokens.For(admin, tenantId),
            new { email = EmailComTamanho(256), password = SenhaForteComTamanho(129), role = (int)ERole.Operator }));

        AssertRequisicaoInvalida(envelope, "Email", "Password");
    }

    [Fact]
    public async Task Create_com_email_de_exatamente_255_e_senha_de_exatamente_128_caracteres_cria_o_operador()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync(ERole.Admin);
        var email = EmailComTamanho(255);
        var senha = SenhaForteComTamanho(128);
        Assert.Equal(255, email.Length);
        Assert.Equal(128, senha.Length);

        var envelope = await ReadAsync(await SendAsync(
            HttpMethod.Post, "v1/account/create", TestTokens.For(admin, tenantId),
            new { email, password = senha, role = (int)ERole.Operator }));

        Assert.Equal(201, envelope.HttpStatus);
    }

    #endregion

    #region Update

    [Fact]
    public async Task Update_com_email_e_senha_acima_do_limite_devolve_400_com_uma_notificacao_por_campo()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync(ERole.Admin);
        var alvo = await Database.SeedOperatorAsync(tenantId, role: ERole.Operator);

        var envelope = await ReadAsync(await SendAsync(
            HttpMethod.Put, $"v1/account/update/{alvo.Id}", TestTokens.For(admin, tenantId),
            new { email = EmailComTamanho(256), password = SenhaForteComTamanho(129) }));

        AssertRequisicaoInvalida(envelope, "Email", "Password");
    }

    #endregion

    #region UpdateMe

    [Fact]
    public async Task UpdateMe_com_email_e_senha_acima_do_limite_devolve_400_com_uma_notificacao_por_campo()
    {
        var (tenantId, op) = await TenantWithOperatorAsync(ERole.Operator);

        var envelope = await ReadAsync(await SendAsync(
            HttpMethod.Put, "v1/account/update-me", TestTokens.For(op, tenantId),
            new { email = EmailComTamanho(256), password = SenhaForteComTamanho(129) }));

        AssertRequisicaoInvalida(envelope, "Email", "Password");
    }

    #endregion
}
