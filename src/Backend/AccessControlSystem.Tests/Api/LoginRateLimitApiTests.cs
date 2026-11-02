using System.Net;
using System.Net.Http.Json;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;

namespace AccessControlSystem.Tests.Api;

// FR-016: o login limita as tentativas por origem e responde 429 "Muitas tentativas. Tente novamente em instantes.",
// com Retry-After. O limite é configurável (LoginProtection:Ip:PermitLimit, padrão 20 por minuto).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class LoginRateLimitApiTests : ApiTestBase
{
    private const string LimiteKey = "LoginProtection:Ip:PermitLimit";

    public LoginRateLimitApiTests(PostgresContainerFixture database) : base(database)
    {
    }

    private Task<HttpResponseMessage> TentarLoginAsync() => LoginAsync("ninguem@test.com", "Errada1!");

    /// <summary>Login de um "cliente final" atrás do proxy (loopback, confiável por padrão), no MESMO limitador da Api.</summary>
    private async Task<int> TentarLoginComoAsync(string clienteFinal)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "v1/account/login")
        {
            Content = JsonContent.Create(new { email = "ninguem@test.com", password = "Errada1!" })
        };
        request.Headers.Add("X-Forwarded-For", clienteFinal);

        using var response = await Client.SendAsync(request);
        return (int)response.StatusCode;
    }

    private async Task<int[]> TentativasAsync(int quantidade)
    {
        var statuses = new List<int>();
        for (var i = 0; i < quantidade; i++)
        {
            using var response = await TentarLoginAsync();
            statuses.Add((int)response.StatusCode);
        }

        return statuses.ToArray();
    }

    // Guarda: fixa o comportamento de hoje, que não tinha teste.
    [Fact]
    public async Task Limite_por_ip_responde_429_com_mensagem_e_retry_after()
    {
        Factory.WithRemoteIp(IPAddress.Parse("203.0.113.10"));

        await TentativasAsync(20);   // o padrão: 20 por minuto
        var response = await TentarLoginAsync();
        var envelope = await ReadAsync(response);

        Assert.Equal(429, envelope.HttpStatus);
        Assert.Equal(429, envelope.Status);
        Assert.Equal("Muitas tentativas. Tente novamente em instantes.", envelope.Message);
        Assert.False(envelope.HasData);   // formato ErrorResponse: sem "data"

        Assert.True(response.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.Parse(values!.Single()) >= 1);
    }

    [Fact]
    public async Task Limite_por_ip_e_configuravel()
    {
        Factory.With(LimiteKey, "2").WithRemoteIp(IPAddress.Parse("203.0.113.11"));

        var statuses = await TentativasAsync(3);

        Assert.Equal([401, 401, 429], statuses);
    }

    [Fact]
    public async Task Limite_por_ip_nao_afeta_outras_origens()
    {
        // Atrás do proxy (loopback), cada cliente final é uma origem; todas dividem o MESMO limitador.
        Factory.With(LimiteKey, "2").WithRemoteIp(IPAddress.Loopback);

        Assert.Equal(401, await TentarLoginComoAsync("198.51.100.1"));
        Assert.Equal(401, await TentarLoginComoAsync("198.51.100.1"));
        Assert.Equal(429, await TentarLoginComoAsync("198.51.100.1"));   // esta origem estourou

        Assert.Equal(401, await TentarLoginComoAsync("198.51.100.2"));   // outra origem segue livre
    }

    // Guarda: sem endereço remoto (o TestServer não tem um), o limite NÃO é desligado: a origem ausente é agrupada de forma estável.
    [Fact]
    public async Task Origem_ausente_continua_limitada()
    {
        var statuses = await TentativasAsync(21);

        Assert.DoesNotContain(429, statuses.Take(20));
        Assert.Equal(429, statuses[20]);
    }

    // Guarda: IPv6 também é limitado, de forma estável, e um IPv6 diferente não é afetado.
    [Fact]
    public async Task Origem_ipv6_e_limitada_de_forma_estavel()
    {
        // O endereço remoto é IPv6 (::1, proxy confiável por padrão): sem X-Forwarded-For, o limite vale para ele.
        Factory.With(LimiteKey, "2").WithRemoteIp(IPAddress.IPv6Loopback);
        var statuses = await TentativasAsync(3);
        Assert.Equal([401, 401, 429], statuses);

        // Um cliente final IPv6 diferente (informado pelo proxy) é outra origem, no mesmo limitador.
        Assert.Equal(401, await TentarLoginComoAsync("2001:db8::1"));
        Assert.Equal(401, await TentarLoginComoAsync("2001:db8::1"));
        Assert.Equal(429, await TentarLoginComoAsync("2001:db8::1"));
        Assert.Equal(401, await TentarLoginComoAsync("2001:db8::2"));
    }
}
