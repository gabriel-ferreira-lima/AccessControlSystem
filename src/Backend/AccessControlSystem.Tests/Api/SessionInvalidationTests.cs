using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Api;

// FR-004 a FR-008 e FR-006: um token deixa de valer NA REQUISIÇÃO SEGUINTE quando a senha do operador é trocada (por ele
// ou por um administrador), quando o perfil muda para outro, ou quando ele é desativado (e reativar não o revalida).
// Trocar só o e-mail, reenviar o mesmo perfil ou deixar a senha em branco não derrubam o token. Os fluxos passam pela
// Api real; o token "antigo" é o emitido antes da mudança.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class SessionInvalidationTests : ApiTestBase
{
    private const string GetMe = "v1/account/get-me";
    private const string NovaSenha = "Nova1!Senha";

    public SessionInvalidationTests(PostgresContainerFixture database) : base(database)
    {
    }

    /// <summary>Tenant novo com um administrador e um operador (perfil escolhido), e o login padrão apontado para ele.</summary>
    private async Task<(Guid TenantId, Operator Admin, Operator Target)> TenantWithAdminAndOperatorAsync(
        ERole targetRole = ERole.Operator)
    {
        var tenantId = await ApiFactory.RegisterTenantAsync(Database);
        var admin = await Database.SeedOperatorAsync(tenantId, role: ERole.Admin);
        var target = await Database.SeedOperatorAsync(tenantId, role: targetRole);
        Factory.WithDefaultTenant(tenantId);
        return (tenantId, admin, target);
    }

    private Task<HttpResponseMessage> PutAsync(string uri, string token, object body) =>
        SendAsync(HttpMethod.Put, uri, token, body);

    private async Task AssertTokenRecusadoAsync(string token)
    {
        var envelope = await ReadAsync(await GetAsync(GetMe, token));

        Assert.Equal(401, envelope.HttpStatus);
        Assert.Equal("Não autenticado", envelope.Message);
    }

    private async Task AssertTokenAceitoAsync(string token)
    {
        var response = await GetAsync(GetMe, token);

        Assert.Equal(200, (int)response.StatusCode);
    }

    private async Task<string> LoginTokenAsync(Operator @operator, string password = OperatorPassword)
    {
        var envelope = await ReadAsync(await LoginAsync(@operator.Email, password));
        Assert.Equal(200, envelope.HttpStatus);
        return envelope.Data.GetProperty("token").GetString()!;
    }

    #region Derrubam o token

    [Fact]
    public async Task Token_antigo_cai_depois_que_o_proprio_operador_troca_a_senha()
    {
        var (tenantId, _, op) = await TenantWithAdminAndOperatorAsync();
        var tokenAntigo = TestTokens.For(op, tenantId);
        await AssertTokenAceitoAsync(tokenAntigo);

        var troca = await PutAsync("v1/account/update-me", tokenAntigo, new { password = NovaSenha });
        Assert.Equal(200, (int)troca.StatusCode);

        await AssertTokenRecusadoAsync(tokenAntigo);
        // Um login com a senha nova emite um token que funciona.
        await AssertTokenAceitoAsync(await LoginTokenAsync(op, NovaSenha));
    }

    [Fact]
    public async Task Token_antigo_cai_depois_que_o_admin_troca_a_senha_do_operador()
    {
        var (tenantId, admin, op) = await TenantWithAdminAndOperatorAsync();
        var tokenAntigo = TestTokens.For(op, tenantId);
        await AssertTokenAceitoAsync(tokenAntigo);

        var troca = await PutAsync($"v1/account/update/{op.Id}", TestTokens.For(admin, tenantId), new { password = NovaSenha });
        Assert.Equal(200, (int)troca.StatusCode);

        await AssertTokenRecusadoAsync(tokenAntigo);
    }

    [Fact]
    public async Task Token_antigo_cai_depois_que_o_admin_muda_o_perfil()
    {
        // Operator -> Admin (o caminho inverso é recusado pela regra de negócio que já existe).
        var (tenantId, admin, op) = await TenantWithAdminAndOperatorAsync(ERole.Operator);
        var tokenAntigo = TestTokens.For(op, tenantId);
        await AssertTokenAceitoAsync(tokenAntigo);

        var troca = await PutAsync($"v1/account/update/{op.Id}", TestTokens.For(admin, tenantId), new { role = (int)ERole.Admin });
        Assert.Equal(200, (int)troca.StatusCode);

        // Mesmo que o novo perfil tenha MAIS permissão, o token emitido antes não vale mais.
        await AssertTokenRecusadoAsync(tokenAntigo);
    }

    // Comportamento que já existe hoje (IsActive a cada requisição), agora com teste (FR-006).
    [Fact]
    public async Task Token_antigo_cai_depois_da_desativacao()
    {
        var (tenantId, admin, op) = await TenantWithAdminAndOperatorAsync();
        var tokenAntigo = TestTokens.For(op, tenantId);
        await AssertTokenAceitoAsync(tokenAntigo);

        var desativa = await PutAsync($"v1/account/deactivate/{op.Id}", TestTokens.For(admin, tenantId), new { });
        Assert.Equal(200, (int)desativa.StatusCode);

        await AssertTokenRecusadoAsync(tokenAntigo);
    }

    [Fact]
    public async Task Token_antigo_continua_recusado_depois_de_desativar_e_reativar()
    {
        var (tenantId, admin, op) = await TenantWithAdminAndOperatorAsync();
        var tokenAntigo = TestTokens.For(op, tenantId);
        var tokenAdmin = TestTokens.For(admin, tenantId);

        Assert.Equal(200, (int)(await PutAsync($"v1/account/deactivate/{op.Id}", tokenAdmin, new { })).StatusCode);
        Assert.Equal(200, (int)(await PutAsync($"v1/account/activate/{op.Id}", tokenAdmin, new { })).StatusCode);

        // Reativar NÃO revalida o token emitido antes da desativação (a desativação já mudou a versão).
        await AssertTokenRecusadoAsync(tokenAntigo);
    }

    [Fact]
    public async Task Login_novo_depois_da_reativacao_emite_token_valido()
    {
        var (tenantId, admin, op) = await TenantWithAdminAndOperatorAsync();
        var tokenAdmin = TestTokens.For(admin, tenantId);

        Assert.Equal(200, (int)(await PutAsync($"v1/account/deactivate/{op.Id}", tokenAdmin, new { })).StatusCode);
        Assert.Equal(200, (int)(await PutAsync($"v1/account/activate/{op.Id}", tokenAdmin, new { })).StatusCode);

        await AssertTokenAceitoAsync(await LoginTokenAsync(op));
    }

    [Fact]
    public async Task Senha_informada_igual_a_atual_derruba_o_token()
    {
        // Decisão do usuário: senha informada (não nula) conta sempre como troca, mesmo igual à atual.
        var (tenantId, admin, op) = await TenantWithAdminAndOperatorAsync();
        var tokenAntigo = TestTokens.For(op, tenantId);

        var troca = await PutAsync($"v1/account/update/{op.Id}", TestTokens.For(admin, tenantId), new { password = OperatorPassword });
        Assert.Equal(200, (int)troca.StatusCode);

        await AssertTokenRecusadoAsync(tokenAntigo);
    }

    [Fact]
    public async Task Duas_trocas_seguidas_derrubam_todo_token_anterior_a_ultima()
    {
        var (tenantId, admin, op) = await TenantWithAdminAndOperatorAsync();
        var tokenAdmin = TestTokens.For(admin, tenantId);
        var tokenAntes = TestTokens.For(op, tenantId);

        // 1ª troca: senha. O token do login seguinte (emitido DEPOIS dela) vale.
        Assert.Equal(200, (int)(await PutAsync($"v1/account/update/{op.Id}", tokenAdmin, new { password = NovaSenha })).StatusCode);
        var tokenIntermediario = await LoginTokenAsync(op, NovaSenha);
        await AssertTokenAceitoAsync(tokenIntermediario);

        // 2ª troca: perfil. Agora cai também o token emitido entre as duas trocas, não só o anterior à primeira.
        Assert.Equal(200, (int)(await PutAsync($"v1/account/update/{op.Id}", tokenAdmin, new { role = (int)ERole.Admin })).StatusCode);

        await AssertTokenRecusadoAsync(tokenAntes);
        await AssertTokenRecusadoAsync(tokenIntermediario);
    }

    #endregion

    #region Não derrubam o token

    [Fact]
    public async Task Troca_so_de_email_nao_derruba_o_token()
    {
        var (tenantId, _, op) = await TenantWithAdminAndOperatorAsync();
        var token = TestTokens.For(op, tenantId);

        var troca = await PutAsync("v1/account/update-me", token, new { email = TenantSeed.UniqueEmail("novo") });
        Assert.Equal(200, (int)troca.StatusCode);

        // A identidade do operador é o Id, não o e-mail (clarificação de 2026-11-01).
        await AssertTokenAceitoAsync(token);
    }

    [Fact]
    public async Task Reenviar_o_mesmo_perfil_e_deixar_a_senha_em_branco_nao_derruba_o_token()
    {
        var (tenantId, admin, op) = await TenantWithAdminAndOperatorAsync(ERole.Operator);
        var token = TestTokens.For(op, tenantId);
        var tokenAdmin = TestTokens.For(admin, tenantId);

        // Mesmo perfil, sem senha.
        Assert.Equal(200, (int)(await PutAsync($"v1/account/update/{op.Id}", tokenAdmin, new { role = (int)ERole.Operator })).StatusCode);
        // Mesmo perfil e senha explicitamente nula (em branco).
        Assert.Equal(200, (int)(await PutAsync($"v1/account/update/{op.Id}", tokenAdmin, new { role = (int)ERole.Operator, password = (string?)null })).StatusCode);

        await AssertTokenAceitoAsync(token);
    }

    [Fact]
    public async Task Troca_da_senha_de_B_nao_derruba_o_token_de_A()
    {
        var (tenantId, admin, operadorB) = await TenantWithAdminAndOperatorAsync();
        var tokenDeA = TestTokens.For(admin, tenantId);
        var tokenDeB = TestTokens.For(operadorB, tenantId);

        Assert.Equal(200, (int)(await PutAsync($"v1/account/update/{operadorB.Id}", tokenDeA, new { password = NovaSenha })).StatusCode);

        await AssertTokenAceitoAsync(tokenDeA);     // quem trocou continua logado
        await AssertTokenRecusadoAsync(tokenDeB);   // só os tokens de B morrem
    }

    #endregion
}
