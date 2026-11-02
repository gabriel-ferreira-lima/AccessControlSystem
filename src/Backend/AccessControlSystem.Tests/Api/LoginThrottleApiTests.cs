using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;
using AccessControlSystem.Tests.Support;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Tests.Api;

// FR-017 a FR-022: depois de 5 tentativas na mesma conta (tenant + e-mail), a 6ª recebe 429 com Retry-After, qualquer que seja
// a origem e mesmo com a senha certa. O limite por origem fica folgado (PermitLimit = 1000) para o limite por CONTA aparecer
// sozinho. O relógio é falso: a espera passa sem ninguém esperar.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class LoginThrottleApiTests : ApiTestBase
{
    private const string ErradaSenha = "Errada1!";
    private const string MensagemDoBloqueio = "Muitas tentativas. Tente novamente em instantes.";

    private readonly FakeTimeProvider _clock = new();

    public LoginThrottleApiTests(PostgresContainerFixture database) : base(database)
    {
    }

    /// <summary>Tenant com um operador, login padrão apontado para ele, relógio falso e limite por origem folgado.</summary>
    private async Task<(Guid TenantId, string Email)> PrepareAsync()
    {
        var (tenantId, op) = await TenantWithOperatorAsync(ERole.Operator);
        Factory.WithDefaultTenant(tenantId).With("LoginProtection:Ip:PermitLimit", "1000").WithFakeTime(_clock);
        return (tenantId, op.Email.Address);
    }

    private async Task<int> StatusAsync(string email, string password)
    {
        using var response = await LoginAsync(email, password);
        return (int)response.StatusCode;
    }

    private async Task<int[]> WrongAttemptsAsync(string email, int count)
    {
        var statuses = new List<int>();
        for (var i = 0; i < count; i++) statuses.Add(await StatusAsync(email, ErradaSenha));
        return statuses.ToArray();
    }

    [Fact]
    public async Task Sexta_tentativa_seguida_na_mesma_conta_recebe_429_com_retry_after_de_60_segundos()
    {
        var (_, email) = await PrepareAsync();

        var cinco = await WrongAttemptsAsync(email, 5);
        using var sexta = await LoginAsync(email, ErradaSenha);
        var envelope = await ReadAsync(sexta);

        Assert.Equal([401, 401, 401, 401, 401], cinco);
        Assert.Equal(429, envelope.HttpStatus);
        Assert.Equal(MensagemDoBloqueio, envelope.Message);
        Assert.Equal(TimeSpan.FromSeconds(60), sexta.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Senha_correta_durante_o_bloqueio_tambem_recebe_429()
    {
        var (_, email) = await PrepareAsync();
        await WrongAttemptsAsync(email, 5);

        Assert.Equal(429, await StatusAsync(email, OperatorPassword));
    }

    [Fact]
    public async Task Email_inexistente_bloqueia_do_mesmo_jeito_e_com_a_mesma_resposta()
    {
        var (_, existente) = await PrepareAsync();
        var inexistente = TenantSeed.UniqueEmail("ninguem");

        await WrongAttemptsAsync(existente, 5);
        await WrongAttemptsAsync(inexistente, 5);
        using var bloqueioExistente = await LoginAsync(existente, ErradaSenha);
        using var bloqueioInexistente = await LoginAsync(inexistente, ErradaSenha);

        // Não revela quais e-mails existem: mesmo status, mesma mensagem, mesmo corpo e mesma espera.
        Assert.Equal(429, (int)bloqueioExistente.StatusCode);
        Assert.Equal(429, (int)bloqueioInexistente.StatusCode);
        Assert.Equal(await bloqueioExistente.Content.ReadAsStringAsync(), await bloqueioInexistente.Content.ReadAsStringAsync());
        Assert.Equal(bloqueioExistente.Headers.RetryAfter?.Delta, bloqueioInexistente.Headers.RetryAfter?.Delta);
    }

    [Fact]
    public async Task Depois_da_espera_a_conta_volta_e_o_proximo_bloqueio_dura_2_minutos()
    {
        var (_, email) = await PrepareAsync();
        await WrongAttemptsAsync(email, 5);
        Assert.Equal(429, await StatusAsync(email, ErradaSenha));

        _clock.Advance(TimeSpan.FromSeconds(61));

        var cinco = await WrongAttemptsAsync(email, 5);
        Assert.Equal([401, 401, 401, 401, 401], cinco);
        using var bloqueada = await LoginAsync(email, ErradaSenha);
        Assert.Equal(429, (int)bloqueada.StatusCode);
        Assert.Equal(TimeSpan.FromMinutes(2), bloqueada.Headers.RetryAfter?.Delta);
    }

    // Guarda: já passa hoje (nunca há 429 por conta); continua valendo depois.
    [Fact]
    public async Task Login_com_sucesso_zera_a_contagem()
    {
        var (_, email) = await PrepareAsync();

        await WrongAttemptsAsync(email, 4);
        Assert.Equal(200, await StatusAsync(email, OperatorPassword));   // o sucesso zera
        var depois = await WrongAttemptsAsync(email, 4);

        Assert.Equal([401, 401, 401, 401], depois);   // sem bloqueio: a contagem recomeçou
    }

    [Fact]
    public async Task Mesmo_email_em_outro_tenant_nao_e_afetado()
    {
        // Duas Apis (um tenant padrão cada) compartilhando o MESMO limitador, como as instâncias farão com o Redis.
        var shared = new InMemoryLoginThrottle(_clock, Options.Create(new LoginThrottleOptions()), NullLogger<InMemoryLoginThrottle>.Instance);
        var (tenantA, operatorA) = await TenantWithOperatorAsync(ERole.Operator);
        var emailA = operatorA.Email.Address;
        var tenantB = await ApiFactory.RegisterTenantAsync(Database);
        await Database.SeedOperatorAsync(tenantB, emailA);   // o MESMO e-mail no tenant B

        Factory.WithDefaultTenant(tenantA).With("LoginProtection:Ip:PermitLimit", "1000").WithSingleton<ILoginThrottle>(shared);
        using var factoryB = new ApiFactory(Database)
            .WithDefaultTenant(tenantB).With("LoginProtection:Ip:PermitLimit", "1000").WithSingleton<ILoginThrottle>(shared);
        using var clientB = factoryB.CreateClient();

        await WrongAttemptsAsync(emailA, 5);
        Assert.Equal(429, await StatusAsync(emailA, ErradaSenha));   // bloqueada no tenant A

        using var emB = await clientB.PostAsJsonAsync("v1/account/login", new { email = emailA, password = ErradaSenha });
        Assert.Equal(401, (int)emB.StatusCode);                       // no tenant B é outra conta: livre
        using var emBCorreta = await clientB.PostAsJsonAsync("v1/account/login", new { email = emailA, password = OperatorPassword });
        Assert.Equal(200, (int)emBCorreta.StatusCode);
    }

    [Fact]
    public async Task Variar_a_caixa_do_email_nao_escapa_do_bloqueio()
    {
        var (_, email) = await PrepareAsync();

        foreach (var variacao in new[] { email.ToUpperInvariant(), email, $"  {email.ToUpperInvariant()} ", email, email.ToUpperInvariant() })
            Assert.Equal(401, await StatusAsync(variacao, ErradaSenha));

        Assert.Equal(429, await StatusAsync(email.ToUpperInvariant(), ErradaSenha));
        Assert.Equal(429, await StatusAsync(email, ErradaSenha));
    }

    // Guarda: e-mail em formato inválido é recusado antes (400) e não entra na contagem de nenhuma conta.
    [Fact]
    public async Task Email_em_formato_invalido_nao_conta_tentativa()
    {
        var (_, email) = await PrepareAsync();

        for (var i = 0; i < 10; i++) Assert.Equal(400, await StatusAsync("nope", ErradaSenha));

        var cinco = await WrongAttemptsAsync(email, 5);
        Assert.Equal([401, 401, 401, 401, 401], cinco);
    }

    [Fact]
    public async Task Corpo_do_429_por_conta_traz_data_nula_e_status_mensagem_e_retry_after_iguais_aos_do_429_por_ip()
    {
        // PermitLimit = 6: as 6 primeiras passam pelo limite por origem; a 6ª já cai no bloqueio por CONTA; a 7ª é barrada
        // pelo limite por ORIGEM. Mesma origem (sem endereço remoto), mesma Api.
        var (_, email) = await PrepareAsync();
        Factory.With("LoginProtection:Ip:PermitLimit", "6");

        await WrongAttemptsAsync(email, 5);
        using var porConta = await LoginAsync(email, ErradaSenha);
        using var porIp = await LoginAsync(email, ErradaSenha);

        var contaEnvelope = await ReadAsync(porConta);
        var ipEnvelope = await ReadAsync(porIp);

        Assert.Equal(429, contaEnvelope.HttpStatus);
        Assert.Equal(429, ipEnvelope.HttpStatus);
        Assert.Equal(ipEnvelope.Status, contaEnvelope.Status);
        Assert.Equal(ipEnvelope.Message, contaEnvelope.Message);
        Assert.Equal(MensagemDoBloqueio, contaEnvelope.Message);
        Assert.NotNull(porConta.Headers.RetryAfter?.Delta);
        Assert.NotNull(porIp.Headers.RetryAfter?.Delta);

        // A única diferença conhecida: o corpo do bloqueio por conta traz "data": null; o do limite por origem não traz "data".
        Assert.True(contaEnvelope.Root.TryGetProperty("data", out var data));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, data.ValueKind);
        Assert.False(ipEnvelope.Root.TryGetProperty("data", out _));
    }

    // O teste de rajada na Api: 50 logins errados AO MESMO TEMPO contra a mesma conta, de origens distintas (para o limite por
    // origem não interferir). A admissão é atômica: só 5 chegam à verificação de senha; as outras 45 recebem 429.
    [Fact]
    public async Task Rajada_simultanea_de_logins_na_mesma_conta_admite_so_cinco_verificacoes()
    {
        var (_, email) = await PrepareAsync();
        Factory.WithRemoteIp(IPAddress.Loopback).WithCountingPasswordVerifier();   // loopback = proxy confiável: o XFF vale

        var gate = new TaskCompletionSource();
        var tasks = Enumerable.Range(1, 50).Select(i => Task.Run(async () =>
        {
            await gate.Task;
            var request = new HttpRequestMessage(HttpMethod.Post, "v1/account/login")
            {
                Content = JsonContent.Create(new { email, password = ErradaSenha })
            };
            request.Headers.Add("X-Forwarded-For", $"198.51.100.{i}");
            using var response = await Client.SendAsync(request);
            return (int)response.StatusCode;
        })).ToArray();

        _ = Client;   // sobe a Api antes de largar
        await Task.Yield();
        gate.SetResult();
        var statuses = await Task.WhenAll(tasks);

        Assert.Equal(5, statuses.Count(s => s == 401));
        Assert.Equal(45, statuses.Count(s => s == 429));
        Assert.True(Factory.PasswordVerifications!.Calls <= 5, $"Verificações de senha: {Factory.PasswordVerifications.Calls} (máximo 5).");
    }

    [Fact]
    public async Task Bloqueio_de_conta_e_logado_sem_email_senha_ou_token()
    {
        var (tenantId, email) = await PrepareAsync();

        await WrongAttemptsAsync(email, 5);          // a 5ª cria o bloqueio
        for (var i = 0; i < 3; i++) await StatusAsync(email, OperatorPassword);   // barradas: não geram novo log

        var avisos = Factory.Logs
            .Where(l => l.Level == LogLevel.Warning && l.Category.EndsWith(nameof(InMemoryLoginThrottle)))
            .ToArray();

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId}:{email}")));
        var aviso = Assert.Single(avisos);
        Assert.Contains(tenantId.ToString(), aviso.Text);
        Assert.Contains(hash[..8], aviso.Text);
        Assert.DoesNotContain(hash, aviso.Text);                                            // nunca o hash completo
        Assert.DoesNotContain(email, aviso.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ErradaSenha, aviso.Text);
        Assert.DoesNotContain(OperatorPassword, aviso.Text);
        Assert.DoesNotContain("token", aviso.Text, StringComparison.OrdinalIgnoreCase);
    }

    // FR-022: se o limitador falhar, o login NÃO é liberado (a exceção vira 500, nunca um token).
    [Fact]
    public async Task Falha_do_throttle_responde_500_e_nunca_libera_o_login()
    {
        var (tenantId, op) = await TenantWithOperatorAsync(ERole.Operator);
        var quebrado = NSubstitute.Substitute.For<ILoginThrottle>();
        quebrado.TryAcquireAttemptAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<TimeSpan?>(_ => throw new InvalidOperationException("throttle indisponível"));
        Factory.WithDefaultTenant(tenantId).WithSingleton(quebrado);

        using var response = await LoginAsync(op.Email.Address, OperatorPassword);   // senha CORRETA

        Assert.Equal(500, (int)response.StatusCode);
        Assert.DoesNotContain("token", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
