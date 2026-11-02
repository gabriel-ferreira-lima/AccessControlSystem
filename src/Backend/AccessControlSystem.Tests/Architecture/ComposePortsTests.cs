using System.Net;
using System.Text.RegularExpressions;

namespace AccessControlSystem.Tests.Architecture;

// FR-026 e FR-023/FR-024 no ambiente local: as portas das Apis e do banco só aceitam conexões da própria máquina, e as Apis
// só confiam nos cabeçalhos de encaminhamento do nginx, que tem IP fixo numa sub-rede fixa. Sem Docker: lê o texto do
// docker-compose.yml da raiz do repositório (falha, não pula, se não achar).
public class ComposePortsTests
{
    private static readonly string[] ServicesWithLocalPorts = ["backend", "backend-b", "backend-dedicated", "db"];
    private static readonly string[] Apis = ["backend", "backend-b", "backend-dedicated"];

    private static string[] ComposeLines()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "docker-compose.yml");
            if (File.Exists(file)) return File.ReadAllLines(file);
        }

        throw new Xunit.Sdk.XunitException("docker-compose.yml não encontrado subindo a partir de " + AppContext.BaseDirectory);
    }

    /// <summary>Linhas do bloco de um serviço (chave com 2 espaços sob <c>services:</c>), sem a linha do nome.</summary>
    private static string[] ServiceBlock(string[] lines, string service)
    {
        var servicesAt = Array.FindIndex(lines, l => l.TrimEnd() == "services:");
        Assert.True(servicesAt >= 0, "Seção services: não encontrada.");

        var start = Array.FindIndex(lines, servicesAt, l => l.TrimEnd() == $"  {service}:");
        Assert.True(start >= 0, $"Serviço '{service}' não encontrado.");

        var block = new List<string>();
        for (var i = start + 1; i < lines.Length; i++)
        {
            // Próxima chave de serviço (2 espaços) ou de seção (coluna 0) encerra o bloco.
            if (Regex.IsMatch(lines[i], @"^( {2}\S|\S)")) break;
            block.Add(lines[i]);
        }

        return block.ToArray();
    }

    private static string[] PortMappings(string[] block)
    {
        var at = Array.FindIndex(block, l => l.Trim() == "ports:");
        Assert.True(at >= 0, "O serviço não publica portas.");

        return block.Skip(at + 1)
            .TakeWhile(l => l.TrimStart().StartsWith("- "))
            .Select(l => l.Trim()[2..].Trim().Trim('"'))
            .ToArray();
    }

    private static string ValueOf(string[] lines, string key)
    {
        var match = lines.Select(l => Regex.Match(l, $@"^\s*(?:- )?{Regex.Escape(key)}:\s*""?([^""\s#]+)""?")).FirstOrDefault(m => m.Success);
        Assert.True(match is not null, $"Chave '{key}' não encontrada.");
        return match!.Groups[1].Value;
    }

    [Fact]
    public void Portas_das_apis_e_do_banco_so_na_maquina_local()
    {
        var lines = ComposeLines();

        foreach (var service in ServicesWithLocalPorts)
        {
            var mappings = PortMappings(ServiceBlock(lines, service));

            Assert.NotEmpty(mappings);
            Assert.All(mappings, mapping => Assert.StartsWith("127.0.0.1:", mapping));
        }
    }

    [Fact]
    public void Console_continua_publicado_como_antes()
    {
        // O FR-026 cita só as Apis e o banco: a porta do console (nginx) não muda.
        var mappings = PortMappings(ServiceBlock(ComposeLines(), "frontend"));

        Assert.All(mappings, mapping => Assert.DoesNotContain("127.0.0.1", mapping));
    }

    [Fact]
    public void Compose_fixa_a_subrede_e_o_ip_do_nginx()
    {
        var lines = ComposeLines();

        var subnet = ValueOf(lines, "subnet");
        var nginxIp = ValueOf(ServiceBlock(lines, "frontend"), "ipv4_address");

        Assert.True(IPNetwork.TryParse(subnet, out var network), $"Sub-rede inválida: {subnet}");
        Assert.True(IPAddress.TryParse(nginxIp, out var address), $"IP inválido: {nginxIp}");
        Assert.True(network.Contains(address), $"{nginxIp} não está dentro de {subnet}.");
    }

    [Fact]
    public void Apis_confiam_so_no_ip_fixo_do_nginx()
    {
        var lines = ComposeLines();
        var nginxIp = ValueOf(ServiceBlock(lines, "frontend"), "ipv4_address");

        // A configuração comum das três Apis (âncora) traz EXATAMENTE um proxy conhecido: o IP fixo do nginx.
        // (linhas de comentário ficam de fora: os comentários do compose citam o nome da chave)
        var config = lines.Where(l => !l.TrimStart().StartsWith('#')).ToArray();
        var proxies = config.Where(l => l.Contains("ForwardedHeaders__KnownProxies__")).ToArray();
        Assert.Single(proxies);
        Assert.Equal(nginxIp, ValueOf(proxies, "ForwardedHeaders__KnownProxies__0"));
        Assert.DoesNotContain(config, l => l.Contains("ForwardedHeaders__KnownNetworks__"));

        // E as três Apis usam essa configuração comum.
        foreach (var api in Apis)
            Assert.Contains(ServiceBlock(lines, api), l => l.Contains("<<: *backend-environment"));
    }
}
