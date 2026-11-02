using System.Xml.Linq;

namespace AccessControlSystem.Tests.Architecture;

// FR-024 a FR-028 / Princípio IX ("Dependências"): a auditoria de pacotes é configuração versionada, então o teste lê os
// arquivos e trava a configuração: NuGet Audit como erro em todos os projetos, a única exceção (SSH.NET) por aviso e só no
// projeto de testes, os Dockerfiles com o Directory.Build.props presente no restore, e os passos de auditoria nos workflows.
// Sem Docker nem rede: lê o texto dos arquivos da raiz do repositório (falha, não pula, se não achar). Contrato:
// specs/20261102-125831-security-perimeter/contracts/dependency-audit.md
public class PackageAuditConfigTests
{
    private static readonly string[] SuppressedAdvisories =
    [
        "https://github.com/advisories/GHSA-q939-rpr3-3284",
        "https://github.com/advisories/GHSA-mggc-4xg6-vcxf",
    ];

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docker-compose.yml"))) return dir.FullName;
        }

        throw new Xunit.Sdk.XunitException("docker-compose.yml não encontrado subindo a partir de " + AppContext.BaseDirectory);
    }

    private static string PathInRepository(string relative) =>
        Path.Combine(RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static string ReadText(string relative)
    {
        var path = PathInRepository(relative);
        Assert.True(File.Exists(path), $"Arquivo esperado não existe: {relative}");
        return File.ReadAllText(path);
    }

    private static IEnumerable<string> Projects() =>
        Directory.EnumerateFiles(PathInRepository("src/Backend"), "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    // Condição efetiva de um elemento de propriedade: a dele ou a do PropertyGroup que o contém.
    private static string? ConditionOf(XElement element) =>
        (string?)element.Attribute("Condition") ?? (string?)element.Parent?.Attribute("Condition");

    #region Directory.Build.props

    [Fact]
    public void Directory_Build_props_liga_o_nuget_audit_em_todos_os_projetos_com_qualquer_severidade()
    {
        var props = XDocument.Parse(ReadText("src/Backend/Directory.Build.props"));

        string? Value(string name) => props.Descendants(name).Select(e => e.Value.Trim()).FirstOrDefault();

        Assert.Equal("true", Value("NuGetAudit"));
        Assert.Equal("all", Value("NuGetAuditMode"));   // inclui os pacotes transitivos
        Assert.Equal("low", Value("NuGetAuditLevel"));   // qualquer severidade
    }

    [Fact]
    public void Aviso_de_vulnerabilidade_vira_erro_sempre_e_a_falha_de_consulta_vira_erro_no_ci()
    {
        var props = XDocument.Parse(ReadText("src/Backend/Directory.Build.props"));
        var asErrors = props.Descendants("WarningsAsErrors").ToList();
        Assert.NotEmpty(asErrors);

        var always = string.Join(";", asErrors.Where(e => ConditionOf(e) is null).Select(e => e.Value));
        foreach (var code in new[] { "NU1901", "NU1902", "NU1903", "NU1904" })
            Assert.Contains(code, always);

        // NU1900 (falha ao consultar a base de avisos) só é erro no CI, para o desenvolvedor sem internet continuar compilando.
        Assert.DoesNotContain("NU1900", always);
        var onCi = asErrors.Where(e => ConditionOf(e) is { } c && c.Contains("'$(CI)' == 'true'", StringComparison.Ordinal)).ToList();
        Assert.Contains(onCi, e => e.Value.Contains("NU1900", StringComparison.Ordinal));
    }

    #endregion

    #region Exceção do SSH.NET (única) e escopo

    [Fact]
    public void Projeto_de_testes_nao_tem_mais_o_NoWarn_geral_do_NU1903_e_tem_so_as_duas_excecoes_por_aviso()
    {
        var csproj = XDocument.Parse(ReadText("src/Backend/AccessControlSystem.Tests/AccessControlSystem.Tests.csproj"));

        Assert.DoesNotContain(csproj.Descendants("NoWarn"), e => e.Value.Contains("NU1903", StringComparison.Ordinal));

        var suppressions = csproj.Descendants("NuGetAuditSuppress").ToList();
        Assert.Equal(
            SuppressedAdvisories.Order(StringComparer.Ordinal).ToArray(),
            suppressions.Select(e => (string)e.Attribute("Include")!).Order(StringComparer.Ordinal).ToArray());

        // A justificativa fica escrita ao lado (comentário XML antes da exceção, no mesmo ItemGroup).
        foreach (var suppression in suppressions)
        {
            var hasComment = suppression.NodesBeforeSelf().OfType<XComment>().Any(c => c.Value.Contains("SSH.NET", StringComparison.Ordinal));
            Assert.True(hasComment, $"A exceção {suppression.Attribute("Include")} está sem o comentário de justificativa (SSH.NET).");
        }
    }

    [Fact]
    public void Nenhum_outro_projeto_tem_excecao_de_auditoria_nem_NoWarn_de_vulnerabilidade()
    {
        foreach (var path in Projects().Where(p => !p.EndsWith("AccessControlSystem.Tests.csproj", StringComparison.Ordinal)))
        {
            var csproj = XDocument.Load(path);

            Assert.Empty(csproj.Descendants("NuGetAuditSuppress"));
            Assert.DoesNotContain(csproj.Descendants("NoWarn"), e => e.Value.Contains("NU190", StringComparison.Ordinal));
        }
    }

    #endregion

    #region Dockerfiles

    [Theory]
    [InlineData("src/Backend/AccessControlSystem.Api/Dockerfile")]
    [InlineData("src/Backend/AccessControlSystem.Migrator/Dockerfile")]
    public void Dockerfile_copia_o_Directory_Build_props_antes_do_restore(string dockerfile)
    {
        // As imagens copiam só os .csproj antes do restore: sem esta linha a auditoria não valeria nelas.
        var lines = ReadText(dockerfile).Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();

        var restore = lines.FindIndex(l => l.StartsWith("RUN dotnet restore", StringComparison.Ordinal));
        var copy = lines.FindIndex(l => l.StartsWith("COPY Directory.Build.props", StringComparison.Ordinal));

        Assert.True(restore >= 0, "Dockerfile sem RUN dotnet restore.");
        Assert.True(copy >= 0, "Dockerfile sem COPY Directory.Build.props.");
        Assert.True(copy < restore, "O COPY Directory.Build.props precisa vir ANTES do primeiro RUN dotnet restore.");
    }

    #endregion

    #region Workflows

    [Fact]
    public void Backend_CI_restaura_e_audita_os_pacotes_antes_do_build()
    {
        var lines = ReadText(".github/workflows/backend-ci.yml").Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();

        var restore = lines.FindIndex(l => l == "run: dotnet restore Backend.slnx");
        var build = lines.FindIndex(l => l.StartsWith("run: dotnet build", StringComparison.Ordinal));

        Assert.True(restore >= 0, "backend-ci.yml sem o passo 'dotnet restore Backend.slnx'.");
        Assert.True(build >= 0, "backend-ci.yml sem o passo de build.");
        Assert.True(restore < build, "O restore (auditoria) precisa vir antes do build.");
    }

    [Fact]
    public void Frontend_CI_audita_qualquer_severidade_e_verifica_os_cabecalhos()
    {
        var lines = ReadText(".github/workflows/frontend-ci.yml").Replace("\r\n", "\n").Split('\n').Select(l => l.Trim()).ToList();

        Assert.Contains("run: npm audit", lines);   // exatamente: sem --audit-level (qualquer severidade)
        Assert.Contains("run: bash scripts/verify-security-headers.sh", lines);
        Assert.DoesNotContain(lines, l => l.Contains("--audit-level", StringComparison.Ordinal));
        Assert.True(
            lines.IndexOf("run: npm ci") < lines.IndexOf("run: npm audit"),
            "O npm audit precisa vir depois do npm ci.");
    }

    #endregion
}
