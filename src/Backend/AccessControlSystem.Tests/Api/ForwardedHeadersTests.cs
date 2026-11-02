using System.Net;
using System.Net.Http.Json;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;

namespace AccessControlSystem.Tests.Api;

// FR-023 a FR-025: a Api só acredita em X-Forwarded-For quando a conexão vem de um proxy conhecido (por configuração);
// de qualquer outra origem o cabeçalho é ignorado e vale o endereço real. O efeito é observado no limite de login por
// origem (PermitLimit = 3 para o teste ser rápido): o IP que o limite usa é o resolvido por esse mecanismo.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class ForwardedHeadersTests : ApiTestBase
{
    private const int Limite = 3;
    private const string LimiteKey = "LoginProtection:Ip:PermitLimit";

    public ForwardedHeadersTests(PostgresContainerFixture database) : base(database)
    {
    }

    /// <summary>Um login (e-mail inexistente) por item, com o X-Forwarded-For dado; devolve os status HTTP em ordem.</summary>
    private static async Task<int[]> LoginsAsync(HttpClient client, params string?[] forwardedFor)
    {
        var statuses = new List<int>();
        foreach (var header in forwardedFor)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "v1/account/login")
            {
                Content = JsonContent.Create(new { email = "ninguem@test.com", password = "Errada1!" })
            };
            if (header is not null) request.Headers.Add("X-Forwarded-For", header);

            using var response = await client.SendAsync(request);
            statuses.Add((int)response.StatusCode);
        }

        return statuses.ToArray();
    }

    private static string?[] Repeat(string? header, int times) => Enumerable.Repeat(header, times).ToArray();

    [Fact]
    public async Task Cabecalho_forjado_de_origem_desconhecida_nao_contorna_o_limite()
    {
        Factory.With(LimiteKey, Limite.ToString()).WithRemoteIp(IPAddress.Parse("203.0.113.50"));

        // Cada tentativa forja um IP novo. Se o cabeçalho valesse, nenhuma seria barrada.
        var statuses = await LoginsAsync(Client, "198.51.100.1", "198.51.100.2", "198.51.100.3", "198.51.100.4", "198.51.100.5", "198.51.100.6");

        Assert.Equal([401, 401, 401, 429, 429, 429], statuses);
    }

    [Fact]
    public async Task Sem_configuracao_so_o_loopback_e_confiavel()
    {
        // Origem que não é loopback: o cabeçalho é ignorado, então o limite vale para o IP real.
        Factory.With(LimiteKey, Limite.ToString()).WithRemoteIp(IPAddress.Parse("10.9.9.9"));
        var deFora = await LoginsAsync(Client, "198.51.100.1", "198.51.100.2", "198.51.100.3", "198.51.100.4");
        Assert.Equal([401, 401, 401, 429], deFora);

        // Loopback (o padrão do framework): é proxy confiável, então cada IP forjado conta como um cliente.
        using var loopback = new ApiFactory(Database).With(LimiteKey, Limite.ToString()).WithRemoteIp(IPAddress.Loopback);
        using var client = loopback.CreateClient();
        var deLoopback = await LoginsAsync(client, "198.51.100.1", "198.51.100.2", "198.51.100.3", "198.51.100.4");
        Assert.DoesNotContain(429, deLoopback);
    }

    [Fact]
    public async Task De_proxy_conhecido_o_ip_do_cliente_final_e_usado()
    {
        Factory.With(LimiteKey, Limite.ToString())
            .With("ForwardedHeaders:KnownProxies:0", "10.0.0.5")
            .WithRemoteIp(IPAddress.Parse("10.0.0.5"));

        // Clientes finais diferentes: nenhum chega ao limite.
        var diferentes = await LoginsAsync(Client, "198.51.100.1", "198.51.100.2", "198.51.100.3", "198.51.100.4", "198.51.100.5", "198.51.100.6");
        Assert.DoesNotContain(429, diferentes);

        // O MESMO cliente final: a 4ª tentativa é barrada, e outro cliente final segue livre.
        var mesmo = await LoginsAsync(Client, Repeat("198.51.100.77", 4));
        Assert.Equal([401, 401, 401, 429], mesmo);
        var outroCliente = await LoginsAsync(Client, "198.51.100.78");
        Assert.Equal([401], outroCliente);
    }

    [Fact]
    public async Task Rede_conhecida_por_cidr_tambem_vale()
    {
        Factory.With(LimiteKey, Limite.ToString())
            .With("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/24")
            .WithRemoteIp(IPAddress.Parse("10.0.0.77"));

        var mesmo = await LoginsAsync(Client, Repeat("198.51.100.77", 4));
        Assert.Equal([401, 401, 401, 429], mesmo);
        var outroCliente = await LoginsAsync(Client, "198.51.100.78");
        Assert.Equal([401], outroCliente);
    }

    [Fact]
    public async Task Com_ForwardLimit_1_enderecos_forjados_a_esquerda_sao_ignorados()
    {
        Factory.With(LimiteKey, Limite.ToString())
            .With("ForwardedHeaders:KnownProxies:0", "10.0.0.5")
            .WithRemoteIp(IPAddress.Parse("10.0.0.5"));

        // Só o último salto (o que o proxy conhecido acrescentou) conta: os dois formam o MESMO cliente, 203.0.113.9.
        var statuses = await LoginsAsync(Client, "1.1.1.1, 203.0.113.9", "2.2.2.2, 203.0.113.9", "3.3.3.3, 203.0.113.9", "4.4.4.4, 203.0.113.9");

        Assert.Equal([401, 401, 401, 429], statuses);
    }

    [Theory]
    [InlineData("ForwardedHeaders:KnownProxies:0", "nao-e-ip")]
    [InlineData("ForwardedHeaders:KnownNetworks:0", "10.0.0.0/99")]
    public void Proxy_ou_rede_invalidos_derrubam_a_partida(string key, string value)
    {
        Factory.With(key, value);

        var exception = Assert.ThrowsAny<Exception>(() => Factory.CreateClient());

        // A mensagem diz qual chave está errada.
        Assert.Contains(key, exception.ToString());
    }
}
