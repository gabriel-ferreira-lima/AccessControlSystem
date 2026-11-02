using AccessControlSystem.Tests.Support;

namespace AccessControlSystem.Tests.Architecture;

// Os testes que rodam scripts (CiScriptsTests, RepositoryProtectionsConfigTests) usam o ScriptShell, que acha o bash por
// regras explícitas: no Windows, ACS_BASH ou o Git Bash (NUNCA o bash do PATH, que lá é o atalho do WSL e não enxerga o
// jq nem o gh do Windows); no Linux, o bash do PATH. Sem bash ou sem jq o teste FALHA com mensagem clara: nunca pula.
// A primeira região prova a regra de localização numa função PURA (nada é executado); a segunda é a fumaça do ScriptShell,
// que roda de verdade nesta máquina.
public class BashLocatorTests
{
    private const string GitBash = @"C:\Program Files\Git\bin\bash.exe";
    private const string WslShortcut = @"C:\Users\gabri\AppData\Local\Microsoft\WindowsApps\bash.exe";

    private sealed class Calls
    {
        public int FindOnPath;
    }

    /// <summary>Roda a função pura com um "sistema de arquivos" e um ambiente simulados.</summary>
    private static string Resolve(
        bool isWindows, string? acsBash = null, string[]? existing = null, string? onPath = null, Calls? calls = null)
    {
        var present = new HashSet<string>(existing ?? [], StringComparer.OrdinalIgnoreCase);

        return BashLocator.Resolve(
            isWindows,
            name => name == "ACS_BASH" ? acsBash : null,
            path => present.Contains(path),
            () =>
            {
                if (calls is not null) calls.FindOnPath++;
                return onPath;
            });
    }

    #region Windows

    [Fact]
    public void Windows_com_ACS_BASH_existente_usa_o_ACS_BASH()
    {
        const string custom = @"D:\ferramentas\git\bin\bash.exe";

        Assert.Equal(custom, Resolve(isWindows: true, acsBash: custom, existing: [custom, GitBash]));
    }

    [Fact]
    public void Windows_com_ACS_BASH_apontando_para_arquivo_inexistente_falha_citando_o_ACS_BASH()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Resolve(isWindows: true, acsBash: @"D:\nao\existe\bash.exe", existing: [GitBash]));

        Assert.Contains("ACS_BASH", exception.Message);
    }

    [Fact]
    public void Windows_sem_ACS_BASH_usa_o_Git_Bash()
    {
        Assert.Equal(GitBash, Resolve(isWindows: true, existing: [GitBash]));
    }

    [Fact]
    public void Windows_sem_ACS_BASH_e_sem_Git_Bash_falha_com_bash_nao_encontrado()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(isWindows: true));

        Assert.Contains("bash não encontrado", exception.Message);
        Assert.Contains("Git para Windows", exception.Message);
        Assert.Contains("ACS_BASH", exception.Message);
    }

    [Theory]
    [InlineData(@"C:\Users\gabri\AppData\Local\Microsoft\WindowsApps\bash.exe")]
    [InlineData(@"c:\users\x\appdata\local\microsoft\windowsapps\bash.exe")]   // sem diferenciar maiúsculas
    [InlineData(@"C:\Windows\System32\bash.exe")]
    [InlineData(@"c:\windows\system32\bash.exe")]
    [InlineData("C:/Windows/System32/bash.exe")]                                // barras normais
    public void Windows_com_ACS_BASH_apontando_para_o_atalho_do_WSL_e_recusado(string wsl)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Resolve(isWindows: true, acsBash: wsl, existing: [wsl, GitBash]));

        Assert.Contains("WSL", exception.Message);
    }

    [Fact]
    public void Windows_nunca_consulta_o_PATH_mesmo_que_o_PATH_tenha_o_bash_do_WSL()
    {
        var calls = new Calls();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Resolve(isWindows: true, onPath: WslShortcut, calls: calls));

        Assert.Equal(0, calls.FindOnPath);   // o PATH não é consultado no Windows
        Assert.Contains("bash não encontrado", exception.Message);
    }

    [Fact]
    public void Windows_com_Git_Bash_ignora_o_PATH_mesmo_que_o_PATH_tenha_outro_bash()
    {
        var calls = new Calls();

        Assert.Equal(GitBash, Resolve(isWindows: true, existing: [GitBash], onPath: @"C:\outro\bash.exe", calls: calls));
        Assert.Equal(0, calls.FindOnPath);
    }

    #endregion

    #region Linux e macOS

    [Fact]
    public void Linux_usa_o_bash_do_PATH()
    {
        Assert.Equal("/usr/bin/bash", Resolve(isWindows: false, onPath: "/usr/bin/bash"));
    }

    [Fact]
    public void Linux_sem_bash_no_PATH_falha()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Resolve(isWindows: false));

        Assert.Contains("bash não encontrado", exception.Message);
    }

    [Fact]
    public void Linux_ignora_o_ACS_BASH()
    {
        Assert.Equal("/usr/bin/bash",
            Resolve(isWindows: false, acsBash: "/custom/bash", existing: ["/custom/bash"], onPath: "/usr/bin/bash"));
    }

    #endregion

    #region jq dentro do bash escolhido

    [Fact]
    public async Task Sem_jq_no_bash_escolhido_falha_com_mensagem_clara()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => ScriptShell.EnsureJqAsync(() => Task.FromResult(false)));

        Assert.Contains("jq não encontrado dentro do bash escolhido", exception.Message);
    }

    [Fact]
    public async Task Com_jq_no_bash_escolhido_nao_falha()
    {
        await ScriptShell.EnsureJqAsync(() => Task.FromResult(true));
    }

    #endregion

    #region Fumaça do ScriptShell (roda de verdade nesta máquina)

    private static string WriteScript(string directory, string name, string content)
    {
        // LF sempre: o bash do Git Bash não aceita CR no fim das linhas.
        File.WriteAllText(
            Path.Combine(ScriptShell.RepositoryRoot(), directory.Replace('/', Path.DirectorySeparatorChar), name),
            content.Replace("\r\n", "\n"));
        return $"{directory}/{name}";
    }

    private static readonly IReadOnlyDictionary<string, string?> NoEnvironment = new Dictionary<string, string?>();

    [Fact]
    public async Task Fumaca_o_script_le_a_entrada_padrao_e_escreve_na_saida_em_utf8()
    {
        var script = WriteScript(ScriptShell.NewScratchDirectory(), "eco.sh", "printf 'olá %s' \"$(cat)\"\n");

        var result = await ScriptShell.RunAsync(script, [], "mundo ção", NoEnvironment);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("olá mundo ção", result.StdOut);
    }

    [Fact]
    public async Task Fumaca_devolve_o_codigo_de_saida_e_a_saida_de_erro()
    {
        var script = WriteScript(ScriptShell.NewScratchDirectory(), "falha.sh", "echo erro-visivel >&2\nexit 3\n");

        var result = await ScriptShell.RunAsync(script, [], null, NoEnvironment);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains("erro-visivel", result.StdErr);
    }

    [Fact]
    public async Task Fumaca_passa_argumentos_e_variaveis_de_ambiente()
    {
        var script = WriteScript(ScriptShell.NewScratchDirectory(), "amb.sh", "printf '%s|%s' \"$1\" \"$ACS_FUMACA\"\n");

        var result = await ScriptShell.RunAsync(script, ["arg um"], null, new Dictionary<string, string?> { ["ACS_FUMACA"] = "valor" });

        Assert.Equal("arg um|valor", result.StdOut);
    }

    [Fact]
    public async Task Fumaca_o_script_roda_com_a_raiz_do_repositorio_como_diretorio_de_trabalho()
    {
        var script = WriteScript(ScriptShell.NewScratchDirectory(), "raiz.sh", "test -f docker-compose.yml\n");

        var result = await ScriptShell.RunAsync(script, [], null, NoEnvironment);

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Fumaca_a_pasta_temporaria_e_relativa_existe_e_o_bash_a_enxerga()
    {
        var directory = ScriptShell.NewScratchDirectory();
        var script = WriteScript(directory, "dir.sh", "test -d \"$1\"\n");

        Assert.False(Path.IsPathRooted(directory));
        Assert.StartsWith("src/Backend/AccessControlSystem.Tests/obj/script-tests/", directory);
        Assert.True(Directory.Exists(Path.Combine(ScriptShell.RepositoryRoot(), directory)));

        var result = await ScriptShell.RunAsync(script, [directory], null, NoEnvironment);

        Assert.Equal(0, result.ExitCode);   // o bash enxerga o MESMO caminho relativo
    }

    [Fact]
    public void Fumaca_cada_chamada_cria_uma_pasta_nova()
    {
        Assert.NotEqual(ScriptShell.NewScratchDirectory(), ScriptShell.NewScratchDirectory());
    }

    #endregion
}
