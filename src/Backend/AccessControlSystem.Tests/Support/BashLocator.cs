namespace AccessControlSystem.Tests.Support;

/// <summary>
/// Acha o <c>bash</c> que os testes de script usam, por regras EXPLÍCITAS. No Windows o <c>bash</c> do PATH é o atalho do
/// WSL (<c>WindowsApps\bash.exe</c> ou <c>System32\bash.exe</c>), que não enxerga o <c>jq</c> nem o <c>gh</c> do Windows: por
/// isso o PATH NUNCA é consultado lá. Vale a variável <c>ACS_BASH</c> (se definida) ou o Git Bash padrão. No Linux e no
/// macOS vale o <c>bash</c> do PATH. Sem <c>bash</c> a localização FALHA com mensagem clara: o teste nunca é pulado.
/// </summary>
/// <remarks>A decisão é uma função PURA (<see cref="Resolve"/>), testada em tabela sem executar nada.</remarks>
public static class BashLocator
{
    /// <summary>Variável que o dono define para apontar um <c>bash</c> específico (só vale no Windows).</summary>
    public const string AcsBashVariable = "ACS_BASH";

    /// <summary>Caminho padrão do Git para Windows.</summary>
    public const string GitBashPath = @"C:\Program Files\Git\bin\bash.exe";

    public static string Resolve(
        bool isWindows,
        Func<string, string?> getEnv,
        Func<string, bool> fileExists,
        Func<string?> findOnPath)
    {
        if (!isWindows)
        {
            // Linux e macOS: o bash do PATH. ACS_BASH não vale aqui.
            var onPath = findOnPath();
            if (string.IsNullOrWhiteSpace(onPath))
                throw new InvalidOperationException("bash não encontrado no PATH. Instale o bash para rodar os testes de script.");

            return onPath;
        }

        var configured = getEnv(AcsBashVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!fileExists(configured))
            {
                throw new InvalidOperationException(
                    $"{AcsBashVariable} aponta para um arquivo que não existe: '{configured}'. Corrija a variável {AcsBashVariable} " +
                    $"ou remova-a para usar o Git Bash padrão ({GitBashPath}).");
            }

            return RejectWslShortcut(configured);
        }

        if (fileExists(GitBashPath))
            return RejectWslShortcut(GitBashPath);

        // O PATH não é consultado no Windows: lá o "bash" costuma ser o atalho do WSL.
        throw new InvalidOperationException(
            "bash não encontrado. No Windows os testes de script usam o bash do Git para Windows " +
            $"({GitBashPath}): instale-o ou defina {AcsBashVariable} com o caminho de um bash que NÃO seja o atalho do WSL.");
    }

    /// <summary>Resolve com o ambiente real desta máquina.</summary>
    public static string Locate() =>
        Resolve(OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable, File.Exists, FindBashOnPath);

    // O atalho do WSL: o lançador em WindowsApps ou o bash.exe de System32 (ou Sysnative, a vista de 64 bits do System32).
    private static string RejectWslShortcut(string path)
    {
        var normalized = path.Replace('/', '\\');

        var isWsl = normalized.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(@"\System32\bash.exe", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(@"\Sysnative\bash.exe", StringComparison.OrdinalIgnoreCase);

        if (isWsl)
        {
            throw new InvalidOperationException(
                $"O bash em '{path}' é o atalho do WSL (Windows Subsystem for Linux), que não serve: não enxerga o jq nem o gh do Windows. " +
                $"Use o bash do Git para Windows ({GitBashPath}) ou aponte {AcsBashVariable} para ele.");
        }

        return path;
    }

    private static string? FindBashOnPath()
    {
        var directories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        return directories
            .Select(directory => Path.Combine(directory, "bash"))
            .FirstOrDefault(File.Exists);
    }
}
