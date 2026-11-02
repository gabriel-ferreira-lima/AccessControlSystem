using System.Diagnostics;
using AccessControlSystem.Api.Health;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Support;
using Microsoft.Extensions.Logging;

namespace AccessControlSystem.Tests.Api;

// FR-019 a FR-023 / SC-007 / SC-008: as duas verificações de saúde são anônimas, mínimas e baratas. A "viva" não consulta
// nada; a "pronta" consulta o catálogo no máximo 1 vez a cada 5 s por instância e responde em até 2 s. O token enviado a
// elas é ignorado, e o resto da Api segue exigindo autenticação como antes (contracts/health-endpoints.md).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class HealthEndpointsTests : ApiTestBase
{
    private const string Live = "health/live";
    private const string Ready = "health/ready";
    private const string GetMe = "v1/account/get-me";
    private const string ConnectionStringInacessivel =
        "Host=127.0.0.1;Port=1;Database=nao_existe;Username=acs_app;Password=x;Timeout=2;Command Timeout=2";

    // O TTL do cache escrito aqui de propósito, independente da constante da produção (pega mudança acidental).
    private static readonly TimeSpan CincoSegundos = TimeSpan.FromSeconds(5);

    public HealthEndpointsTests(PostgresContainerFixture database) : base(database)
    {
    }

    #region Viva

    [Fact]
    public async Task Viva_responde_200_healthy_em_texto_puro_sem_cache()
    {
        using var response = await GetAsync(Live);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", CacheControl(response));
    }

    [Theory]
    [InlineData(Live)]
    [InlineData(Ready)]
    public async Task Head_responde_200_sem_cache(string route)
    {
        // O corpo do HEAD não é afirmado: quem o suprime é o servidor (o Kestrel o faz; o TestServer não).
        using var response = await SendAsync(HttpMethod.Head, route, token: null);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Contains("no-store", CacheControl(response));
    }

    [Fact]
    public async Task Viva_continua_200_com_a_sonda_do_catalogo_lancando_excecao()
    {
        var probe = new ProbeFalsa { Comportamento = _ => Task.FromException(new InvalidOperationException("catálogo fora")) };
        Factory.WithSingleton<ICatalogProbe>(probe);

        using var response = await GetAsync(Live);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, probe.Calls);   // a viva não consulta nada
    }

    [Fact]
    public async Task Viva_continua_200_com_o_catalogo_apontado_para_uma_porta_fechada()
    {
        Factory.With("ConnectionStrings:Catalog", ConnectionStringInacessivel);

        using var response = await GetAsync(Live);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Pronta

    [Fact]
    public async Task Pronta_com_o_catalogo_real_responde_200_healthy()
    {
        using var response = await GetAsync(Ready);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Contains("no-store", CacheControl(response));
    }

    [Fact]
    public async Task Pronta_com_a_sonda_lancando_excecao_responde_503_sem_vazar_a_mensagem_nem_no_log()
    {
        const string mensagem = "Host=segredo;Password=x";
        var probe = new ProbeFalsa { Comportamento = _ => Task.FromException(new InvalidOperationException(mensagem)) };
        Factory.WithSingleton<ICatalogProbe>(probe);

        using var response = await GetAsync(Ready);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("Unhealthy", body);
        Assert.Contains("no-store", CacheControl(response));
        Assert.DoesNotContain("segredo", body);

        // O log traz só o NOME DO TIPO da exceção: nem a mensagem, nem o texto da exceção.
        Assert.DoesNotContain(Factory.Logs, log => log.Text.Contains("segredo") || log.Text.Contains("Password=x"));
        Assert.Single(Factory.Logs, log =>
            log.Level == LogLevel.Warning && log.Text.Contains(nameof(InvalidOperationException)));
    }

    [Fact]
    public async Task Pronta_com_o_catalogo_sem_esquema_responde_503()
    {
        var catalogoSemMigrations = await Database.CreateIsolatedCatalogAsync(migrate: false);
        Factory.With("ConnectionStrings:Catalog", catalogoSemMigrations.App);

        using var response = await GetAsync(Ready);

        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }

    // O tempo limite de 2 s corre em tempo REAL (o relógio falso só controla o TTL do cache), então este teste espera ≈2 s.
    [Fact]
    public async Task Pronta_com_a_sonda_que_nunca_termina_responde_503_em_cerca_de_2_segundos()
    {
        var probe = new ProbeFalsa { Comportamento = ct => Task.Delay(Timeout.Infinite, ct) };
        Factory.WithSingleton<ICatalogProbe>(probe);

        var relogio = Stopwatch.StartNew();
        using var response = await GetAsync(Ready);
        relogio.Stop();

        Assert.Equal(503, (int)response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
        Assert.InRange(relogio.Elapsed.TotalSeconds, 1.5, 6.0);
    }

    [Fact]
    public async Task Pronta_responde_200_mesmo_com_as_bases_de_tenant_inacessiveis()
    {
        Factory.WithTenantDatabase(PostgresContainerFixture.SharedCatalogDatabaseName, ConnectionStringInacessivel);
        Factory.WithTenantDatabase(PostgresContainerFixture.DedicatedCatalogDatabaseName, ConnectionStringInacessivel);

        using var response = await GetAsync(Ready);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    #endregion

    #region Cache de 5 segundos (SC-007 e SC-008): conta chamadas, não mede tempo

    [Fact]
    public async Task Pronta_faz_uma_so_consulta_em_mil_chamadas_dentro_de_5_segundos()
    {
        var relogio = new FakeTimeProvider();
        var probe = new ProbeFalsa();
        Factory.WithFakeTime(relogio).WithSingleton<ICatalogProbe>(probe);

        // 500 em sequência...
        for (var i = 0; i < 500; i++)
        {
            using var response = await GetAsync(Ready);
            Assert.Equal(200, (int)response.StatusCode);
        }

        // ...e 500 concorrentes, sem o relógio andar: o resultado em cache vale para todas.
        var concorrentes = await Task.WhenAll(Enumerable.Range(0, 500).Select(_ => GetAsync(Ready)));
        Assert.All(concorrentes, r => Assert.Equal(200, (int)r.StatusCode));

        Assert.Equal(1, probe.Calls);
    }

    [Fact]
    public async Task Pronta_faz_a_segunda_consulta_depois_de_5_segundos()
    {
        var relogio = new FakeTimeProvider();
        var probe = new ProbeFalsa();
        Factory.WithFakeTime(relogio).WithSingleton<ICatalogProbe>(probe);

        using (await GetAsync(Ready)) { }
        using (await GetAsync(Ready)) { }
        Assert.Equal(1, probe.Calls);

        relogio.Advance(CincoSegundos - TimeSpan.FromMilliseconds(1));
        using (await GetAsync(Ready)) { }
        Assert.Equal(1, probe.Calls);   // ainda dentro dos 5 s

        relogio.Advance(TimeSpan.FromMilliseconds(1));
        using (await GetAsync(Ready)) { }
        Assert.Equal(2, probe.Calls);   // venceu
    }

    [Fact]
    public async Task Pronta_com_cache_vencido_e_chamadas_concorrentes_faz_uma_so_consulta()
    {
        var relogio = new FakeTimeProvider();
        var probe = new ProbeFalsa();
        Factory.WithFakeTime(relogio).WithSingleton<ICatalogProbe>(probe);

        using (await GetAsync(Ready)) { }   // aquece: 1ª consulta
        relogio.Advance(CincoSegundos);

        // A consulta que vai refazer o cache fica presa até o teste soltá-la.
        var portao = new TaskCompletionSource();
        probe.Comportamento = ct => portao.Task.WaitAsync(ct);

        var chamadas = Enumerable.Range(0, 20).Select(_ => GetAsync(Ready)).ToArray();
        await EsperarAsync(() => probe.Calls == 2);
        await Task.Delay(300);   // dá tempo de as outras 19 chegarem e esperarem
        Assert.Equal(2, probe.Calls);

        portao.SetResult();
        var respostas = await Task.WhenAll(chamadas);

        Assert.All(respostas, r => Assert.Equal(200, (int)r.StatusCode));
        Assert.Equal(2, probe.Calls);   // 1 do aquecimento + 1 só da rodada concorrente
    }

    [Fact]
    public async Task Pronta_guarda_a_falha_por_5_segundos_e_reflete_a_volta_do_catalogo_em_ate_5_segundos()
    {
        var relogio = new FakeTimeProvider();
        var probe = new ProbeFalsa { Comportamento = _ => Task.FromException(new InvalidOperationException("fora")) };
        Factory.WithFakeTime(relogio).WithSingleton<ICatalogProbe>(probe);

        using (var primeira = await GetAsync(Ready))
        {
            Assert.Equal(503, (int)primeira.StatusCode);
        }

        probe.Comportamento = _ => Task.CompletedTask;   // o catálogo voltou
        using (var emCache = await GetAsync(Ready))
        {
            Assert.Equal(503, (int)emCache.StatusCode);   // a falha também fica em cache por 5 s
        }
        Assert.Equal(1, probe.Calls);

        relogio.Advance(CincoSegundos);
        using (var depois = await GetAsync(Ready))
        {
            Assert.Equal(200, (int)depois.StatusCode);   // reflete a volta
        }
        Assert.Equal(2, probe.Calls);
    }

    [Fact]
    public async Task Pronta_reflete_a_queda_do_catalogo_em_ate_5_segundos()
    {
        var relogio = new FakeTimeProvider();
        var probe = new ProbeFalsa();
        Factory.WithFakeTime(relogio).WithSingleton<ICatalogProbe>(probe);

        using (var primeira = await GetAsync(Ready))
        {
            Assert.Equal(200, (int)primeira.StatusCode);
        }

        probe.Comportamento = _ => Task.FromException(new InvalidOperationException("caiu"));   // o catálogo caiu
        using (var emCache = await GetAsync(Ready))
        {
            Assert.Equal(200, (int)emCache.StatusCode);   // ainda dentro dos 5 s
        }

        relogio.Advance(CincoSegundos);
        using (var depois = await GetAsync(Ready))
        {
            Assert.Equal(503, (int)depois.StatusCode);   // reflete a queda
        }
    }

    #endregion

    #region Métodos

    // Só GET e HEAD executam a verificação. Outro método cai no endpoint de rejeição do roteamento (405), que NÃO é
    // anônimo: sem token, a política de fallback da Api (fechada por padrão) o transforma em 401, como em qualquer outra
    // rota da Api chamada com o método errado; com token válido, responde 405. Nos dois casos a verificação não executa.
    [Theory]
    [InlineData(Live)]
    [InlineData(Ready)]
    public async Task Post_sem_token_nao_executa_a_verificacao_e_responde_401(string route)
    {
        var probe = new ProbeFalsa();
        Factory.WithSingleton<ICatalogProbe>(probe);

        using var response = await SendAsync(HttpMethod.Post, route, token: null);

        Assert.Equal(401, (int)response.StatusCode);
        Assert.DoesNotContain("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, probe.Calls);
    }

    [Theory]
    [InlineData(Live)]
    [InlineData(Ready)]
    public async Task Post_com_token_valido_responde_405_sem_executar_a_verificacao(string route)
    {
        var probe = new ProbeFalsa();
        Factory.WithSingleton<ICatalogProbe>(probe);
        var (tenantId, admin) = await TenantWithOperatorAsync();

        using var response = await SendAsync(HttpMethod.Post, route, TestTokens.For(admin, tenantId));

        Assert.Equal(405, (int)response.StatusCode);
        Assert.Equal(0, probe.Calls);
    }

    #endregion

    #region O Authorization é ignorado só nas duas rotas de saúde (U2)

    // Token inválido ou válido, enviado às rotas de saúde, não muda o resultado e não dispara NENHUMA consulta de
    // sessão nem de catálogo: o validador e o resolvedor contam as chamadas e o teste exige zero.
    [Theory]
    [InlineData(Live, false)]
    [InlineData(Live, true)]
    [InlineData(Ready, false)]
    [InlineData(Ready, true)]
    public async Task Authorization_enviado_a_saude_nao_muda_o_resultado_nem_consulta_sessao_ou_catalogo(
        string route, bool tokenValido)
    {
        var probe = new ProbeFalsa();
        var sessao = new ValidadorDeSessaoFalso();
        var tenants = new ResolvedorDeTenantFalso();
        Factory.WithSingleton<ICatalogProbe>(probe)
            .WithSingleton<ISessionValidator>(sessao)
            .WithSingleton<ITenantResolver>(tenants);

        string token = "lixo";
        if (tokenValido)
        {
            var (tenantId, admin) = await TenantWithOperatorAsync();
            token = TestTokens.For(admin, tenantId);
        }

        using var response = await GetAsync(route, token);

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Equal(0, sessao.Calls);
        Assert.Equal(0, tenants.Calls);
        Assert.Equal(route == Live ? 0 : 1, probe.Calls);   // a viva nunca consulta; a pronta, uma vez
    }

    // Controle de não vacuidade: a MESMA instrumentação enxerga a chamada quando o token vai a um endpoint protegido.
    // Sem isto, "zero chamadas" nas rotas de saúde poderia só significar que o contador não funciona.
    [Fact]
    public async Task Controle_o_mesmo_token_em_endpoint_protegido_aciona_a_validacao()
    {
        var sessao = new ValidadorDeSessaoFalso();
        var tenants = new ResolvedorDeTenantFalso();
        Factory.WithSingleton<ISessionValidator>(sessao).WithSingleton<ITenantResolver>(tenants);
        var (tenantId, admin) = await TenantWithOperatorAsync();

        using var response = await GetAsync(GetMe, TestTokens.For(admin, tenantId));

        Assert.Equal(1, tenants.Calls);
        Assert.Equal(1, sessao.Calls);
    }

    // Não regressão da mudança do JWT (T011): o resto da Api segue como antes. Com a validação NORMAL, um endpoint
    // protegido aceita o token válido e recusa o inválido (e a falta de token).
    [Fact]
    public async Task Endpoint_protegido_segue_aceitando_token_valido_e_recusando_invalido()
    {
        var (tenantId, admin) = await TenantWithOperatorAsync();

        using var valido = await GetAsync(GetMe, TestTokens.For(admin, tenantId));
        Assert.Equal(200, (int)valido.StatusCode);

        var invalido = await ReadAsync(await GetAsync(GetMe, "lixo"));
        Assert.Equal(401, invalido.HttpStatus);
        Assert.Equal("Não autenticado", invalido.Message);

        var semToken = await ReadAsync(await GetAsync(GetMe));
        Assert.Equal(401, semToken.HttpStatus);
    }

    #endregion

    #region Apoio

    private static string CacheControl(HttpResponseMessage response) =>
        string.Join(",", response.Headers.TryGetValues("Cache-Control", out var values) ? values : []);

    private static async Task EsperarAsync(Func<bool> condicao)
    {
        var limite = Stopwatch.StartNew();
        while (!condicao())
        {
            Assert.True(limite.Elapsed < TimeSpan.FromSeconds(1.5), "A condição esperada não aconteceu a tempo.");
            await Task.Delay(10);
        }
    }

    /// <summary>Sonda do catálogo falsa: conta as chamadas e tem comportamento trocável (passa, lança, trava).</summary>
    private sealed class ProbeFalsa : ICatalogProbe
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Func<CancellationToken, Task> Comportamento { get; set; } = _ => Task.CompletedTask;

        public Task ProbeAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return Comportamento(cancellationToken);
        }
    }

    private sealed class ValidadorDeSessaoFalso : ISessionValidator
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<bool> IsValidAsync(Guid operatorId, int securityVersion, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(true);
        }
    }

    private sealed class ResolvedorDeTenantFalso : ITenantResolver
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<bool> ResolveAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(true);
        }
    }

    #endregion
}
