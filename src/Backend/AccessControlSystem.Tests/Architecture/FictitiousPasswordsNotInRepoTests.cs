using System.Text.Json;
using System.Text.RegularExpressions;

namespace AccessControlSystem.Tests.Architecture;

// SC-010: nenhuma senha de operador fictício no repositório (que é PÚBLICO). Escopo exato: código de produção,
// appsettings*.json, docker-compose*.yml e .github/. Ficam de fora os testes (podem ter senhas literais de teste, como o
// SeedDevCommandTests) e o .env.example (só tem nomes de variável). O varredor é uma função pura sobre (caminho, linhas),
// e o teste tem checagem de não vacuidade e controle negativo: sem eles, "nenhum achado" poderia só significar que o
// varredor não enxerga os arquivos. Falha, não pula, se não achar a raiz do repositório.
public class FictitiousPasswordsNotInRepoTests
{
    private static readonly string[] ProductionProjects =
        ["Api", "Application", "Domain", "Infrastructure", "Migrator"];

    private sealed record Finding(string Path, int Line, string Text);

    /// <summary>O que o varredor de YAML viu: todas as chaves de senha de dados fictícios e os achados (valor literal).</summary>
    private sealed record YamlScan(IReadOnlyList<string> Keys, IReadOnlyList<Finding> Findings);

    #region Varredor (função pura)

    // Atribuição (com "=") de literal NÃO vazio a uma das três senhas. "==", declaração, string.Empty e mensagem não casam.
    private static readonly Regex CSharpPasswordLiteral = new(
        "\\b(?:AdminPassword|OperatorPasswordTenant[AB])\\b\\s*=\\s*\"[^\"]+\"",
        RegexOptions.Compiled);

    // Chave de senha de dado fictício em compose ou workflow, no formato de mapa (chave: valor) ou de lista (chave=valor).
    private static readonly Regex YamlPasswordEntry = new(
        @"(?<key>(?:DevSeed|StagingSeed)__[A-Za-z0-9_]*Password[A-Za-z0-9_]*)\s*[:=]\s*(?<value>.*)$",
        RegexOptions.Compiled);

    private static readonly Regex YamlTrailingComment = new(@"\s+#.*$", RegexOptions.Compiled);

    private static IReadOnlyList<Finding> ScanCSharp(string path, IReadOnlyList<string> lines)
    {
        var findings = new List<Finding>();
        for (var i = 0; i < lines.Count; i++)
        {
            if (CSharpPasswordLiteral.IsMatch(lines[i]))
                findings.Add(new Finding(path, i + 1, "atribuição de senha literal"));
        }
        return findings;
    }

    // Só as seções DevSeed e StagingSeed contam, e só a chave com "Password" no nome; ausente, nula ou "" é permitido.
    private static IReadOnlyList<Finding> ScanJson(string path, string text)
    {
        var options = new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        using var document = JsonDocument.Parse(text, options);

        var findings = new List<Finding>();
        if (document.RootElement.ValueKind != JsonValueKind.Object) return findings;

        foreach (var section in document.RootElement.EnumerateObject())
        {
            var isSeedSection =
                string.Equals(section.Name, "DevSeed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(section.Name, "StagingSeed", StringComparison.OrdinalIgnoreCase);
            if (!isSeedSection || section.Value.ValueKind != JsonValueKind.Object) continue;

            foreach (var entry in section.Value.EnumerateObject())
            {
                if (!entry.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)) continue;

                var empty = entry.Value.ValueKind == JsonValueKind.Null
                    || (entry.Value.ValueKind == JsonValueKind.String && string.IsNullOrEmpty(entry.Value.GetString()));
                if (!empty) findings.Add(new Finding(path, 0, $"{section.Name}:{entry.Name} preenchida"));
            }
        }
        return findings;
    }

    // Permitido: valor vazio ou referência (${VARIAVEL} do compose, ${{ expressão }} de workflow). Literal é achado.
    private static YamlScan ScanYaml(string path, IReadOnlyList<string> lines)
    {
        var keys = new List<string>();
        var findings = new List<Finding>();

        for (var i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith('#')) continue;

            var match = YamlPasswordEntry.Match(lines[i]);
            if (!match.Success) continue;

            keys.Add(match.Groups["key"].Value);

            var value = YamlTrailingComment.Replace(match.Groups["value"].Value, string.Empty).Trim().Trim('"', '\'');
            if (value.Length > 0 && !value.StartsWith("${", StringComparison.Ordinal))
                findings.Add(new Finding(path, i + 1, $"{match.Groups["key"].Value} com valor literal"));
        }

        return new YamlScan(keys, findings);
    }

    #endregion

    #region Arquivos do escopo

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docker-compose.yml"))) return dir.FullName;
        }

        throw new Xunit.Sdk.XunitException("docker-compose.yml não encontrado subindo a partir de " + AppContext.BaseDirectory);
    }

    private static bool IsBuildOutput(string path) =>
        path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj");

    private static string[] ProductionCSharpFiles()
    {
        var backend = Path.Combine(RepositoryRoot(), "src", "Backend");
        return ProductionProjects
            .SelectMany(project => Directory.EnumerateFiles(Path.Combine(backend, $"AccessControlSystem.{project}"), "*.cs", SearchOption.AllDirectories))
            .Where(file => !IsBuildOutput(file))
            .ToArray();
    }

    private static string[] AppSettingsFiles()
    {
        var backend = Path.Combine(RepositoryRoot(), "src", "Backend");
        return Directory.EnumerateFiles(backend, "appsettings*.json", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file) && !file.Contains("AccessControlSystem.Tests"))
            .ToArray();
    }

    private static string[] ComposeAndWorkflowFiles()
    {
        var root = RepositoryRoot();
        var compose = Directory.EnumerateFiles(root, "docker-compose*.y*ml", SearchOption.TopDirectoryOnly);
        var github = Directory.Exists(Path.Combine(root, ".github"))
            ? Directory.EnumerateFiles(Path.Combine(root, ".github"), "*.y*ml", SearchOption.AllDirectories)
            : [];
        return compose.Concat(github).ToArray();
    }

    #endregion

    #region Repositório real

    [Fact]
    public void Codigo_de_producao_nao_atribui_senha_ficticia_literal()
    {
        var findings = ProductionCSharpFiles()
            .SelectMany(file => ScanCSharp(file, File.ReadAllLines(file)))
            .ToArray();

        Assert.True(findings.Length == 0, Describe(findings));
    }

    [Fact]
    public void Appsettings_nao_tem_senha_ficticia_preenchida()
    {
        var findings = AppSettingsFiles()
            .SelectMany(file => ScanJson(file, File.ReadAllText(file)))
            .ToArray();

        Assert.True(findings.Length == 0, Describe(findings));
    }

    [Fact]
    public void Compose_e_github_so_referenciam_variaveis_nas_senhas_ficticias()
    {
        var findings = ComposeAndWorkflowFiles()
            .SelectMany(file => ScanYaml(file, File.ReadAllLines(file)).Findings)
            .ToArray();

        Assert.True(findings.Length == 0, Describe(findings));
    }

    private static string Describe(IReadOnlyList<Finding> findings) =>
        "Senha fictícia literal no repositório (SC-010): " + string.Join("; ", findings.Select(f => $"{f.Path}:{f.Line}"));

    #endregion

    #region Não vacuidade e controle negativo

    // Se o varredor não enxergasse os arquivos, os três testes acima passariam à toa.
    [Fact]
    public void Nao_vacuidade_o_varredor_enxerga_as_tres_entradas_DevSeed_do_compose_e_o_codigo_de_producao()
    {
        var compose = Path.Combine(RepositoryRoot(), "docker-compose.yml");
        var keys = ScanYaml(compose, File.ReadAllLines(compose)).Keys;

        Assert.Contains(keys, key => key.EndsWith("DevSeed__AdminPassword", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.EndsWith("DevSeed__OperatorPasswordTenantA", StringComparison.Ordinal));
        Assert.Contains(keys, key => key.EndsWith("DevSeed__OperatorPasswordTenantB", StringComparison.Ordinal));

        var csharp = ProductionCSharpFiles();
        Assert.NotEmpty(csharp);
        Assert.Contains(csharp, file => file.EndsWith("DevSeedOptions.cs", StringComparison.Ordinal));
        Assert.NotEmpty(AppSettingsFiles());
        Assert.NotEmpty(ComposeAndWorkflowFiles());
    }

    [Fact]
    public void Controle_negativo_senha_literal_sintetica_e_apontada_e_referencia_ou_vazio_nao()
    {
        // C#: literal não vazio é apontado; vazio, string.Empty, declaração e mensagem de validação não.
        var csharp = ScanCSharp("sintetico.cs",
        [
            "options.AdminPassword = \"Abc123!x\";",
            "var s = new StagingSeedOptions { OperatorPasswordTenantA = \"Outra!123\" };",
            "public string AdminPassword { get; set; } = string.Empty;",
            "options.AdminPassword = \"\";",
            "options.OperatorPasswordTenantB = string.Empty;",
            "[Required(ErrorMessage = \"DevSeed:AdminPassword é obrigatória.\")]",
            "var senha = _seed.AdminPassword;",
        ]);
        Assert.Equal(new[] { 1, 2 }, csharp.Select(f => f.Line).ToArray());

        // YAML (compose e workflows): literal é apontado, com ou sem aspas, em mapa ou em lista; ${...} e vazio não.
        var yaml = ScanYaml("sintetico.yml",
        [
            "      DevSeed__AdminPassword: Abc123!x",
            "      - StagingSeed__OperatorPasswordTenantA=literal",
            "      DevSeed__OperatorPasswordTenantB: \"Aspas!123\"",
            "      DevSeed__AdminPassword: ${DEV_ADMIN_PASSWORD:?defina no .env}",
            "      StagingSeed__AdminPassword: ${{ secrets.QUALQUER }}",
            "      DevSeed__OperatorPasswordTenantA:",
            "      DevSeed__OperatorPasswordTenantA: \"\"",
            "      # DevSeed__AdminPassword: comentario",
        ]);
        Assert.Equal(new[] { 1, 2, 3 }, yaml.Findings.Select(f => f.Line).ToArray());
        Assert.Equal(7, yaml.Keys.Count);   // viu as sete chaves reais (a linha de comentário não conta)

        // JSON: só as seções DevSeed e StagingSeed contam, e só o valor não vazio.
        Assert.Single(ScanJson("sintetico.json", "{\"DevSeed\":{\"AdminPassword\":\"abc\"}}"));
        Assert.Empty(ScanJson("sintetico.json", "{\"StagingSeed\":{\"AdminPassword\":\"\"}}"));
        Assert.Empty(ScanJson("sintetico.json", "{\"DevSeed\":{}}"));
        Assert.Empty(ScanJson("sintetico.json", "{\"Outra\":{\"Password\":\"x\"}}"));
    }

    #endregion
}
