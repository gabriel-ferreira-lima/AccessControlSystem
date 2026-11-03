namespace AccessControlSystem.Tests.Support;

/// <summary>
/// Um <c>gh</c> FALSO para os testes dos scripts de <c>scripts/github/</c>: um script <c>bash</c> numa pasta temporária que
/// REGISTRA cada chamada (os argumentos, uma chamada por linha) e devolve a resposta enlatada cujo padrão casa com os
/// argumentos. NUNCA fala com o GitHub: os testes passam o caminho dele em <c>GH_BIN</c>, e é só assim que os scripts o usam
/// (o padrão dos scripts é o <c>gh</c> de verdade, que o implementador nunca roda; o <c>--dry-run</c> real é do dono).
/// </summary>
/// <remarks>
/// <para>
/// O padrão de cada resposta é um <i>glob</i> do <c>bash</c> comparado com TODOS os argumentos juntos por espaço (por
/// exemplo <c>api repos/{owner}/{repo}/rulesets</c> ou <c>api -X PUT repos/{owner}/{repo}/rulesets/1234 --input -</c>);
/// vale a primeira resposta registrada que casa e ainda tem usos. <see cref="RespondOnce"/> serve para respostas que mudam
/// com o tempo (por exemplo, a releitura depois de um <c>PATCH</c>). Chamada sem resposta enlatada sai com <c>99</c> e uma
/// mensagem, para o teste enxergar uma chamada inesperada.
/// </para>
/// <para>
/// A pasta fica em <see cref="ScriptShell.NewScratchDirectory"/> (caminho relativo à raiz, ignorado pelo git) e é removida
/// no <see cref="Dispose"/>. O corpo enviado por <c>--input -</c> fica guardado e sai por <see cref="InputOf"/>.
/// </para>
/// </remarks>
public sealed class FakeGh : IDisposable
{
    private const string Script = """
        #!/usr/bin/env bash
        # gh FALSO dos testes: registra a chamada e devolve a resposta enlatada. Nunca fala com o GitHub.
        dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
        joined="$*"
        printf '%s\n' "$joined" >> "$dir/calls.log"
        index="$(wc -l < "$dir/calls.log" | tr -d ' \r')"

        # O corpo enviado por --input - fica guardado para o teste conferir.
        case " $joined " in
          *" --input - "*) cat > "$dir/input-$index.json" ;;
        esac

        for rule in "$dir"/rules/*/; do
          [[ -d "$rule" ]] || continue
          pattern="$(cat "$rule/pattern")"
          uses="$(cat "$rule/uses")"
          if [[ "$uses" == "0" ]]; then
            continue
          fi
          case "$joined" in
            $pattern)
              if [[ "$uses" -gt 0 ]]; then
                printf '%s' "$((uses - 1))" > "$rule/uses"
              fi
              code="$(cat "$rule/code")"
              if [[ "$code" == "0" ]]; then
                cat "$rule/body"
              else
                cat "$rule/body" >&2
              fi
              exit "$code"
              ;;
          esac
        done

        printf 'gh falso: sem resposta enlatada para: %s\n' "$joined" >&2
        exit 99
        """;

    private readonly string _root;
    private int _rules;

    public FakeGh()
    {
        Folder = ScriptShell.NewScratchDirectory();
        _root = Path.Combine(ScriptShell.RepositoryRoot(), Folder.Replace('/', Path.DirectorySeparatorChar));
        System.IO.Directory.CreateDirectory(Path.Combine(_root, "rules"));

        var path = Path.Combine(_root, "gh");
        File.WriteAllText(path, Script.Replace("\r\n", "\n") + "\n");   // LF sempre: o bash não aceita CR
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    /// <summary>A pasta do <c>gh</c> falso, relativa à raiz do repositório (com barras normais).</summary>
    public string Folder { get; }

    /// <summary>O valor de <c>GH_BIN</c> que faz um script usar este <c>gh</c> falso (caminho relativo à raiz).</summary>
    public string GhPath => $"{Folder}/gh";

    /// <summary>
    /// Registra uma resposta. <paramref name="exitCode"/> diferente de zero escreve o corpo na saída de ERRO, como o
    /// <c>gh</c> real faz; <paramref name="uses"/> é quantas vezes ela vale (<c>-1</c> = sempre).
    /// </summary>
    public FakeGh Respond(string pattern, string body, int exitCode = 0, int uses = -1)
    {
        var rule = Path.Combine(_root, "rules", (++_rules).ToString("D3"));
        System.IO.Directory.CreateDirectory(rule);
        File.WriteAllText(Path.Combine(rule, "pattern"), pattern);
        File.WriteAllText(Path.Combine(rule, "body"), body);
        File.WriteAllText(Path.Combine(rule, "code"), exitCode.ToString());
        File.WriteAllText(Path.Combine(rule, "uses"), uses.ToString());
        return this;
    }

    /// <summary>Registra uma resposta que vale uma única vez (as seguintes caem na próxima resposta que casa).</summary>
    public FakeGh RespondOnce(string pattern, string body, int exitCode = 0) => Respond(pattern, body, exitCode, uses: 1);

    /// <summary>Todas as chamadas feitas ao <c>gh</c> falso, em ordem (os argumentos juntos por espaço).</summary>
    public IReadOnlyList<string> Calls
    {
        get
        {
            var log = Path.Combine(_root, "calls.log");
            return File.Exists(log)
                ? File.ReadAllLines(log).Select(line => line.TrimEnd('\r')).ToArray()
                : [];
        }
    }

    /// <summary>Só as chamadas de ESCRITA (<c>POST</c>, <c>PUT</c>, <c>PATCH</c> ou <c>DELETE</c>).</summary>
    public IReadOnlyList<string> Writes =>
        Calls.Where(call => call.Split(' ').Zip(call.Split(' ').Skip(1)).Any(pair =>
            pair.First == "-X" && pair.Second is "POST" or "PUT" or "PATCH" or "DELETE")).ToArray();

    /// <summary>O corpo que a chamada de índice <paramref name="callIndex"/> (a partir de 0) enviou por <c>--input -</c>; <c>null</c> se não enviou.</summary>
    public string? InputOf(int callIndex)
    {
        var path = Path.Combine(_root, $"input-{callIndex + 1}.json");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public void Dispose()
    {
        try
        {
            System.IO.Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Sobra só uma pasta temporária ignorada pelo git.
        }
    }
}
