using System.Diagnostics;
using System.Text;

namespace AccessControlSystem.Tests.Support;

/// <summary>O que um script de shell devolveu: código de saída e o texto das duas saídas.</summary>
public sealed record ScriptResult(int ExitCode, string StdOut, string StdErr);

/// <summary>
/// Roda scripts de shell do repositório nos testes, sempre pelo <c>bash</c> escolhido pelo <see cref="BashLocator"/>, com
/// o diretório de trabalho na RAIZ do repositório e o caminho do script RELATIVO a ela (igual para o Git Bash e para o
/// .NET). Antes da primeira execução confere que o <c>jq</c> existe DENTRO do <c>bash</c> escolhido; sem <c>bash</c> ou
/// sem <c>jq</c> o teste FALHA com mensagem clara, nunca pula.
/// </summary>
public static class ScriptShell
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);
    private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);

    // A localização e a conferência do jq acontecem uma vez. Lazy guarda também a falha: todo teste seguinte repete a mesma
    // mensagem clara, em vez de cada um tentar de novo.
    private static readonly Lazy<string> Bash = new(BashLocator.Locate);
    private static readonly Lazy<Task> JqCheck = new(() => EnsureJqAsync(ProbeJqAsync));

    /// <summary>Roda <paramref name="scriptRelativePath"/> (relativo à raiz do repositório) com argumentos, entrada padrão e variáveis.</summary>
    /// <param name="environment">Variáveis a definir; valor <c>null</c> REMOVE a variável do ambiente do script.</param>
    public static async Task<ScriptResult> RunAsync(
        string scriptRelativePath,
        string[] arguments,
        string? stdin,
        IReadOnlyDictionary<string, string?> environment)
    {
        await JqCheck.Value;

        return await RunBashAsync([scriptRelativePath, .. arguments], stdin, environment);
    }

    /// <summary>Falha com mensagem clara se a sonda disser que o <c>jq</c> não existe no <c>bash</c> escolhido.</summary>
    public static async Task EnsureJqAsync(Func<Task<bool>> jqProbe)
    {
        if (!await jqProbe())
        {
            throw new InvalidOperationException(
                "jq não encontrado dentro do bash escolhido. Instale o jq de modo que o bash dos testes o enxergue " +
                "(no Windows: 'winget install jqlang.jq', e abra um terminal novo).");
        }
    }

    /// <summary>
    /// Cria uma pasta nova e vazia em <c>src/Backend/AccessControlSystem.Tests/obj/script-tests/&lt;Guid&gt;/</c> e devolve o
    /// caminho RELATIVO à raiz (com barras normais). Fica sob o <c>obj/</c> do projeto de testes, que o
    /// <c>src/Backend/.gitignore</c> ignora (o <c>obj/</c> da raiz não é ignorado, por isso não serve).
    /// </summary>
    public static string NewScratchDirectory()
    {
        var relative = $"src/Backend/AccessControlSystem.Tests/obj/script-tests/{Guid.NewGuid():N}";
        Directory.CreateDirectory(Path.Combine(RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar)));
        return relative;
    }

    /// <summary>A raiz do repositório (onde está o <c>docker-compose.yml</c>). FALHA, não pula, se não achar.</summary>
    public static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docker-compose.yml"))) return dir.FullName;
        }

        throw new Xunit.Sdk.XunitException("docker-compose.yml não encontrado subindo a partir de " + AppContext.BaseDirectory);
    }

    private static async Task<bool> ProbeJqAsync()
    {
        var result = await RunBashAsync(["-c", "command -v jq"], stdin: null, new Dictionary<string, string?>());
        return result.ExitCode == 0;
    }

    private static async Task<ScriptResult> RunBashAsync(
        IEnumerable<string> arguments, string? stdin, IReadOnlyDictionary<string, string?> environment)
    {
        var startInfo = new ProcessStartInfo(Bash.Value)
        {
            WorkingDirectory = RepositoryRoot(),
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = Utf8WithoutBom,
            StandardOutputEncoding = Utf8WithoutBom,
            StandardErrorEncoding = Utf8WithoutBom,
        };

        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        foreach (var (name, value) in environment)
        {
            if (value is null) startInfo.Environment.Remove(name);
            else startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Não foi possível iniciar o bash '{Bash.Value}'.");

        // Começa a ler as saídas ANTES de escrever a entrada: evita impasse se o script escrever bastante.
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();

        try
        {
            if (stdin is not null) await process.StandardInput.WriteAsync(stdin);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // O script terminou sem ler a entrada: não é erro do teste.
        }

        using var timeout = new CancellationTokenSource(Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException($"O script não terminou em {Timeout.TotalSeconds:0} s: {string.Join(' ', arguments)}");
        }

        return new ScriptResult(process.ExitCode, await stdout, await stderr);
    }
}
