using AccessControlSystem.Tests.Support;

namespace AccessControlSystem.Tests.Architecture;

// Os scripts que decidem o gate rodam aqui DE VERDADE (pelo ScriptShell: Git Bash no Windows, bash do PATH no Linux):
//  (a) scripts/ci/detect-areas.sh     lista de arquivos alterados -> backend, frontend, docs_only
//  (b) scripts/ci/evaluate-gate.sh    resultado de cada verificação + áreas -> veredito e tabela do resumo
//  (c) scripts/github/check-rules.sh  resposta do endpoint de regras da main -> as proteções estão no lugar?
// Contratos: contracts/ci-gate.md e contracts/repository-protections.md; áreas em data-model.md, seção 1.
public class CiScriptsTests
{
    private const string DetectAreas = "scripts/ci/detect-areas.sh";
    private const string EvaluateGate = "scripts/ci/evaluate-gate.sh";
    private const string CheckRules = "scripts/github/check-rules.sh";

    private static readonly IReadOnlyDictionary<string, string?> NoEnvironment = new Dictionary<string, string?>();

    #region (a) detect-areas.sh

    // nome do caso, arquivos alterados, backend, frontend, docs_only
    public static TheoryData<string, string[], bool, bool, bool> AreaCases => new()
    {
        // Só documentos: não exigem backend nem frontend.
        { "so_specs", ["specs/x/spec.md"], false, false, true },
        { "readme_da_raiz", ["README.md"], false, false, true },
        { "claude_md", ["CLAUDE.md"], false, false, true },
        { "constituicao", [".specify/memory/constitution.md"], false, false, true },
        { "skill", [".claude/skills/x/SKILL.md"], false, false, true },
        { "md_dentro_do_backend", ["src/Backend/README.md"], false, false, true },   // a regra *.md vence a do backend
        { "md_dentro_do_frontend", ["src/Frontend/README.md"], false, false, true },
        { "varios_documentos", ["specs/a/spec.md", "README.md", ".claude/x.md"], false, false, true },

        // Uma área.
        { "so_backend", ["src/Backend/A.cs"], true, false, false },
        { "so_frontend", ["src/Frontend/src/a.ts"], false, true, false },
        { "frontend_com_espaco_no_nome", ["src/Frontend/src/a b.ts"], false, true, false },
        { "backend_e_frontend", ["src/Backend/A.cs", "src/Frontend/src/a.ts"], true, true, false },

        // Fora das áreas conhecidas: fechado por padrão, exige TUDO.
        { "docker_compose", ["docker-compose.yml"], true, true, false },
        { "sql_do_docker", ["docker/postgres/x.sql"], true, true, false },
        { "workflow", [".github/workflows/x.yml"], true, true, false },
        { "script_de_ci", ["scripts/ci/x.sh"], true, true, false },
        { "gitignore", [".gitignore"], true, true, false },
        { "gitattributes", [".gitattributes"], true, true, false },
        { "raiz_sem_extensao", ["LICENSE"], true, true, false },

        // Misturas.
        { "documentos_mais_github", ["specs/a/spec.md", ".github/x.yml"], true, true, false },
        { "documentos_mais_backend", ["specs/a/spec.md", "src/Backend/A.cs"], true, false, false },
        { "documentos_mais_frontend", ["specs/a/spec.md", "src/Frontend/x.ts"], false, true, false },
        { "frontend_mais_fora_das_areas", ["src/Frontend/x.ts", "docker-compose.yml"], true, true, false },

        // Lista vazia: tudo aplicável (nunca "nenhuma").
        { "lista_vazia", [], true, true, false },
    };

    [Theory]
    [MemberData(nameof(AreaCases))]
    public async Task Detect_areas_classifica_os_arquivos(string caso, string[] files, bool backend, bool frontend, bool docsOnly)
    {
        var result = await ScriptShell.RunAsync(DetectAreas, [], Stdin(files), NoEnvironment);

        Assert.True(result.ExitCode == 0, $"[{caso}] saiu {result.ExitCode}: {result.StdErr}");
        Assert.Equal(
            new Dictionary<string, string>
            {
                ["backend"] = Bool(backend),
                ["frontend"] = Bool(frontend),
                ["docs_only"] = Bool(docsOnly),
            },
            KeyValues(result.StdOut));
    }

    [Fact]
    public async Task Detect_areas_ignora_o_cr_do_fim_de_linha_e_as_linhas_em_branco()
    {
        var result = await ScriptShell.RunAsync(DetectAreas, [], "specs/x/spec.md\r\n\r\nREADME.md\r\n\n", NoEnvironment);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("false", KeyValues(result.StdOut)["backend"]);
        Assert.Equal("true", KeyValues(result.StdOut)["docs_only"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\n\n  \n")]
    public async Task Detect_areas_com_so_linhas_em_branco_conta_como_lista_vazia(string stdin)
    {
        var result = await ScriptShell.RunAsync(DetectAreas, [], stdin, NoEnvironment);

        Assert.Equal(0, result.ExitCode);
        var values = KeyValues(result.StdOut);
        Assert.Equal("true", values["backend"]);
        Assert.Equal("true", values["frontend"]);
        Assert.Equal("false", values["docs_only"]);
    }

    [Fact]
    public async Task Detect_areas_nunca_declara_nenhuma_area_sem_ser_so_documentos()
    {
        // Estado incoerente (reprova no gate): backend=false, frontend=false e docs_only=false. O script nunca o produz.
        foreach (var files in new[] { "x\n", "src/a\n", "Backend/A.cs\n", "src/backend/A.cs\n" })
        {
            var values = KeyValues((await ScriptShell.RunAsync(DetectAreas, [], files, NoEnvironment)).StdOut);

            Assert.False(values["backend"] == "false" && values["frontend"] == "false" && values["docs_only"] == "false",
                $"Estado incoerente para a entrada '{files.Trim()}'.");
        }
    }

    private static string Stdin(string[] files) => files.Length == 0 ? string.Empty : string.Join("\n", files) + "\n";

    private static string Bool(bool value) => value ? "true" : "false";

    /// <summary>Linhas <c>chave=valor</c> da saída; falha se a saída trouxer qualquer outra coisa.</summary>
    private static Dictionary<string, string> KeyValues(string stdout)
    {
        var lines = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.All(lines, line => Assert.Contains('=', line));
        return lines.Select(l => l.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);
    }

    #endregion

    #region (b) evaluate-gate.sh

    private const string K6Row = "k6 curto (não ativado)";

    /// <summary>
    /// <c>toJSON(needs)</c> simulado. <c>"-"</c> omite a chave (um job que nem apareceu em <c>needs</c>).
    /// </summary>
    private static string Needs(string changes = "success", string backend = "success", string frontend = "success", string k6 = "success")
    {
        var parts = new List<string>();
        void Add(string key, string result)
        {
            if (result != "-") parts.Add($"\"{key}\":{{\"result\":\"{result}\",\"outputs\":{{}}}}");
        }

        Add("changes", changes);
        Add("backend", backend);
        Add("frontend", frontend);
        Add("k6-short", k6);
        return "{" + string.Join(",", parts) + "}";
    }

    private sealed record Gate(ScriptResult Result, string Summary)
    {
        public int ExitCode => Result.ExitCode;

        /// <summary>Estado e motivo da linha da tabela do resumo cujo nome é <paramref name="name"/>.</summary>
        public (string State, string Reason) Row(string name)
        {
            foreach (var line in Summary.Split('\n'))
            {
                var cells = line.Split('|').Select(c => c.Trim()).ToArray();
                // "| nome | estado | motivo |" vira ["", nome, estado, motivo, ""].
                if (cells.Length >= 5 && cells[1] == name) return (cells[2], cells[3]);
            }

            throw new Xunit.Sdk.XunitException($"Linha '{name}' não encontrada no resumo:\n{Summary}");
        }

        public string LastLine => Summary.Split('\n').Select(l => l.TrimEnd('\r')).Last(l => l.Length > 0);
    }

    /// <summary>Roda o avaliador com o resumo apontado para um arquivo (relativo à raiz) e devolve também o texto dele.</summary>
    private static async Task<Gate> EvaluateAsync(string needs, string? backend, string? frontend, string? docsOnly)
    {
        var summary = $"{ScriptShell.NewScratchDirectory()}/summary.md";

        var result = await ScriptShell.RunAsync(EvaluateGate, [], null, new Dictionary<string, string?>
        {
            ["NEEDS_JSON"] = needs,
            ["AREA_BACKEND"] = backend,
            ["AREA_FRONTEND"] = frontend,
            ["AREA_DOCS_ONLY"] = docsOnly,
            ["GITHUB_STEP_SUMMARY"] = summary,
        });

        var path = Path.Combine(ScriptShell.RepositoryRoot(), summary);
        return new Gate(result, File.Exists(path) ? File.ReadAllText(path) : string.Empty);
    }

    // caso; resultado de changes, backend, frontend e k6-short ("-" = chave ausente); áreas (null = variável não definida);
    // código de saída esperado; estado esperado das linhas backend e frontend ("*" = não conferir)
    [Theory]
    // Tudo aplicável e tudo certo.
    [InlineData("tudo_aprovado", "success", "success", "success", "success", "true", "true", "false", 0, "aprovada", "aprovada")]
    // Backend aplicável: só success passa.
    [InlineData("backend_failure", "success", "failure", "success", "success", "true", "true", "false", 1, "reprovada", "aprovada")]
    [InlineData("backend_cancelled", "success", "cancelled", "success", "success", "true", "true", "false", 1, "reprovada", "aprovada")]
    [InlineData("backend_skipped_aplicavel", "success", "skipped", "success", "success", "true", "true", "false", 1, "reprovada", "aprovada")]
    // Backend dispensado: só skipped; qualquer outro resultado é incoerente.
    [InlineData("backend_dispensado", "success", "skipped", "success", "success", "false", "true", "false", 0, "dispensada", "aprovada")]
    [InlineData("backend_dispensado_com_success", "success", "success", "success", "success", "false", "true", "false", 1, "reprovada", "aprovada")]
    [InlineData("backend_dispensado_com_failure", "success", "failure", "success", "success", "false", "true", "false", 1, "reprovada", "aprovada")]
    // Frontend, igual.
    [InlineData("frontend_failure", "success", "success", "failure", "success", "true", "true", "false", 1, "aprovada", "reprovada")]
    [InlineData("frontend_cancelled", "success", "success", "cancelled", "success", "true", "true", "false", 1, "aprovada", "reprovada")]
    [InlineData("frontend_skipped_aplicavel", "success", "success", "skipped", "success", "true", "true", "false", 1, "aprovada", "reprovada")]
    [InlineData("frontend_dispensado", "success", "success", "skipped", "success", "true", "false", "false", 0, "aprovada", "dispensada")]
    [InlineData("frontend_dispensado_com_success", "success", "success", "success", "success", "true", "false", "false", 1, "aprovada", "reprovada")]
    // Só documentos: as duas dispensadas e o gate aprova.
    [InlineData("so_documentos", "success", "skipped", "skipped", "success", "false", "false", "true", 0, "dispensada", "dispensada")]
    // Mudança que toca só um lado: o outro é dispensado.
    [InlineData("so_backend", "success", "success", "skipped", "success", "true", "false", "false", 0, "aprovada", "dispensada")]
    // O job changes decide tudo: só success.
    [InlineData("changes_failure", "failure", "success", "success", "success", "true", "true", "false", 1, "*", "*")]
    [InlineData("changes_cancelled", "cancelled", "success", "success", "success", "true", "true", "false", 1, "*", "*")]
    [InlineData("changes_skipped", "skipped", "success", "success", "success", "true", "true", "false", 1, "*", "*")]
    // O passo k6 curto: só success (e aparece como "não ativado").
    [InlineData("k6_failure", "success", "success", "success", "failure", "true", "true", "false", 1, "aprovada", "aprovada")]
    [InlineData("k6_skipped", "success", "success", "success", "skipped", "true", "true", "false", 1, "aprovada", "aprovada")]
    [InlineData("k6_cancelled", "success", "success", "success", "cancelled", "true", "true", "false", 1, "aprovada", "aprovada")]
    // Chave ausente em needs: nunca aprova por falta de resultado.
    [InlineData("chave_changes_ausente", "-", "success", "success", "success", "true", "true", "false", 1, "*", "*")]
    [InlineData("chave_backend_ausente", "success", "-", "success", "success", "true", "true", "false", 1, "*", "*")]
    [InlineData("chave_frontend_ausente", "success", "success", "-", "success", "true", "true", "false", 1, "*", "*")]
    [InlineData("chave_k6_ausente", "success", "success", "success", "-", "true", "true", "false", 1, "*", "*")]
    // Área vazia, inválida ou indefinida.
    [InlineData("area_backend_vazia", "success", "success", "success", "success", "", "true", "false", 1, "*", "*")]
    [InlineData("area_frontend_talvez", "success", "success", "success", "success", "true", "talvez", "false", 1, "*", "*")]
    [InlineData("area_docs_talvez", "success", "success", "success", "success", "true", "true", "talvez", 1, "*", "*")]
    [InlineData("area_backend_indefinida", "success", "success", "success", "success", null, "true", "false", 1, "*", "*")]
    [InlineData("area_frontend_indefinida", "success", "success", "success", "success", "true", null, "false", 1, "*", "*")]
    [InlineData("area_docs_indefinida", "success", "success", "success", "success", "true", "true", null, 1, "*", "*")]
    // Estado incoerente: nenhuma área e não é "só documentos".
    [InlineData("tres_false", "success", "skipped", "skipped", "success", "false", "false", "false", 1, "*", "*")]
    public async Task Evaluate_gate_decide_pelo_resultado_real_de_cada_verificacao(
        string caso, string changes, string backend, string frontend, string k6,
        string? areaBackend, string? areaFrontend, string? areaDocs,
        int expectedExit, string backendState, string frontendState)
    {
        var gate = await EvaluateAsync(Needs(changes, backend, frontend, k6), areaBackend, areaFrontend, areaDocs);

        Assert.True(gate.ExitCode == expectedExit, $"[{caso}] saiu {gate.ExitCode} (esperado {expectedExit}): {gate.Result.StdErr}\n{gate.Summary}");
        Assert.Equal(expectedExit == 0 ? "Resultado do gate: APROVADO" : "Resultado do gate: REPROVADO", gate.LastLine);

        if (backendState != "*") Assert.Equal(backendState, gate.Row("backend").State);
        if (frontendState != "*") Assert.Equal(frontendState, gate.Row("frontend").State);
    }

    [Fact]
    public async Task Evaluate_gate_mostra_o_passo_k6_como_nao_ativado_e_aprova_com_ele()
    {
        var gate = await EvaluateAsync(Needs(), "true", "true", "false");

        Assert.Equal(0, gate.ExitCode);
        var (state, reason) = gate.Row(K6Row);   // o nome da linha é exatamente "k6 curto (não ativado)"
        Assert.Equal("não ativado", state);
        Assert.NotEqual("aprovada", state);       // nunca aparece como aprovado
        Assert.Contains("spec 06", reason);
    }

    [Fact]
    public async Task Evaluate_gate_explica_a_dispensa_e_lista_todas_as_linhas_mesmo_na_reprovacao()
    {
        var gate = await EvaluateAsync(Needs(backend: "failure", frontend: "skipped"), "true", "false", "false");

        Assert.Equal(1, gate.ExitCode);
        Assert.Equal("aprovada", gate.Row("changes").State);
        Assert.Equal("reprovada", gate.Row("backend").State);
        Assert.Equal("dispensada", gate.Row("frontend").State);
        Assert.Contains("frontend", gate.Row("frontend").Reason);   // diz qual área não mudou
        Assert.Equal("não ativado", gate.Row(K6Row).State);
    }

    [Fact]
    public async Task Evaluate_gate_reprova_quando_o_needs_nao_e_um_json_valido()
    {
        var gate = await EvaluateAsync("isto não é json", "true", "true", "false");

        Assert.Equal(1, gate.ExitCode);
    }

    [Fact]
    public async Task Evaluate_gate_sem_GITHUB_STEP_SUMMARY_escreve_a_tabela_na_saida_padrao()
    {
        var result = await ScriptShell.RunAsync(EvaluateGate, [], null, new Dictionary<string, string?>
        {
            ["NEEDS_JSON"] = Needs(),
            ["AREA_BACKEND"] = "true",
            ["AREA_FRONTEND"] = "true",
            ["AREA_DOCS_ONLY"] = "false",
            ["GITHUB_STEP_SUMMARY"] = null,   // remove a variável: fora do Actions
        });

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("| Verificação | Estado | Motivo |", result.StdOut);
        Assert.Contains(K6Row, result.StdOut);
        Assert.Contains("Resultado do gate: APROVADO", result.StdOut);
    }

    #endregion

    #region (c) check-rules.sh

    private const string Source =
        "\"ruleset_source_type\":\"Repository\",\"ruleset_source\":\"gabriel-ferreira-lima/AccessControlSystem\",\"ruleset_id\":1234";

    private static string PullRequestRule() =>
        "{\"type\":\"pull_request\"," + Source + ",\"parameters\":{\"required_approving_review_count\":0," +
        "\"dismiss_stale_reviews_on_push\":false,\"require_code_owner_review\":false," +
        "\"require_last_push_approval\":false,\"required_review_thread_resolution\":false}}";

    // checks = o conteúdo do array required_status_checks, escrito em JSON.
    private static string StatusChecksRule(string checks) =>
        "{\"type\":\"required_status_checks\"," + Source + ",\"parameters\":{\"required_status_checks\":[" + checks +
        "],\"strict_required_status_checks_policy\":false}}";

    private static string PlainRule(string type) => "{\"type\":\"" + type + "\"," + Source + "}";

    private const string GoodCheck = "{\"context\":\"ci-gate\",\"integration_id\":15368}";

    private static string Response(params string[] rules) => "[" + string.Join(",", rules) + "]";

    private static string FullResponse(string checks = GoodCheck) =>
        Response(PullRequestRule(), StatusChecksRule(checks), PlainRule("non_fast_forward"), PlainRule("deletion"));

    private static async Task<(int ExitCode, string Output)> CheckAsync(string stdin)
    {
        var result = await ScriptShell.RunAsync(CheckRules, [], stdin, NoEnvironment);
        return (result.ExitCode, result.StdOut + result.StdErr);
    }

    private static int Faltas(string output) =>
        output.Split('\n').Count(line => line.TrimStart().StartsWith("falta:", StringComparison.Ordinal));

    [Fact]
    public async Task Check_rules_aprova_a_resposta_completa_e_correta()
    {
        var (exit, output) = await CheckAsync(FullResponse());

        Assert.True(exit == 0, output);
        Assert.Equal(0, Faltas(output));
    }

    [Fact]
    public async Task Check_rules_aprova_com_um_check_a_mais_alem_do_ci_gate()
    {
        var (exit, output) = await CheckAsync(FullResponse(GoodCheck + ",{\"context\":\"outro\",\"integration_id\":15368}"));

        Assert.True(exit == 0, output);
    }

    [Theory]
    [InlineData("sem_pull_request", "pull_request")]
    [InlineData("sem_required_status_checks", "required_status_checks")]
    [InlineData("sem_non_fast_forward", "non_fast_forward")]
    [InlineData("sem_deletion", "deletion")]
    public async Task Check_rules_reprova_e_diz_qual_regra_falta(string caso, string missing)
    {
        var rules = new Dictionary<string, string>
        {
            ["pull_request"] = PullRequestRule(),
            ["required_status_checks"] = StatusChecksRule(GoodCheck),
            ["non_fast_forward"] = PlainRule("non_fast_forward"),
            ["deletion"] = PlainRule("deletion"),
        };
        rules.Remove(missing);

        var (exit, output) = await CheckAsync(Response(rules.Values.ToArray()));

        Assert.True(exit == 1, $"[{caso}] {output}");
        Assert.Contains($"falta: {missing}", output);
        Assert.Equal(1, Faltas(output));
    }

    // O ruleset existe, mas o check exigido está errado: nome diferente, sem origem, ou de outro aplicativo (um status
    // externo com o mesmo nome passaria a valer).
    [Theory]
    [InlineData("nome_errado", "{\"context\":\"outro-nome\",\"integration_id\":15368}")]
    [InlineData("sem_integration_id", "{\"context\":\"ci-gate\"}")]
    [InlineData("integration_id_de_outro_app", "{\"context\":\"ci-gate\",\"integration_id\":99999}")]
    [InlineData("integration_id_nulo", "{\"context\":\"ci-gate\",\"integration_id\":null}")]
    [InlineData("integration_id_como_texto", "{\"context\":\"ci-gate\",\"integration_id\":\"15368\"}")]
    [InlineData("nome_com_outras_maiusculas", "{\"context\":\"CI-Gate\",\"integration_id\":15368}")]
    [InlineData("nenhum_check", "")]
    public async Task Check_rules_reprova_o_check_com_nome_ou_origem_errados(string caso, string checks)
    {
        var (exit, output) = await CheckAsync(FullResponse(checks));

        Assert.True(exit == 1, $"[{caso}] {output}");
        Assert.Contains("falta: required_status_checks", output);
    }

    [Fact]
    public async Task Check_rules_com_a_lista_vazia_reprova_com_as_quatro_faltas()
    {
        var (exit, output) = await CheckAsync("[]");

        Assert.Equal(1, exit);
        Assert.Equal(4, Faltas(output));
    }

    // Não conseguir ler as regras (permissão, limite, erro) NUNCA é aprovar.
    [Theory]
    [InlineData("objeto_de_erro_403", "{\"message\":\"Forbidden\",\"status\":\"403\"}")]
    [InlineData("objeto_vazio", "{}")]
    [InlineData("entrada_vazia", "")]
    [InlineData("so_espacos", "  \n ")]
    [InlineData("nao_e_json", "isto não é json")]
    [InlineData("json_de_texto", "\"Forbidden\"")]
    [InlineData("null", "null")]
    public async Task Check_rules_reprova_quando_a_entrada_nao_e_uma_lista_de_regras(string caso, string stdin)
    {
        var (exit, output) = await CheckAsync(stdin);

        Assert.True(exit == 1, $"[{caso}] {output}");
        Assert.False(string.IsNullOrWhiteSpace(output), "A reprovação precisa explicar o motivo.");
    }

    // O nome e o integration_id esperados vêm de main-ruleset.json, nunca duplicados no script: uma cópia do script com
    // outro main-ruleset.json (nome e integration_id diferentes) passa a esperar os valores novos.
    [Fact]
    public async Task Check_rules_le_o_nome_e_o_integration_id_do_main_ruleset_json()
    {
        var directory = ScriptShell.NewScratchDirectory();
        var root = ScriptShell.RepositoryRoot();
        var scratch = Path.Combine(root, directory.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.Combine(scratch, "protections"));

        // O script lê o arquivo ao lado de si: protections/main-ruleset.json.
        File.WriteAllText(
            Path.Combine(scratch, "check-rules.sh"),
            File.ReadAllText(Path.Combine(root, "scripts", "github", "check-rules.sh")).Replace("\r\n", "\n"));
        File.WriteAllText(
            Path.Combine(scratch, "protections", "main-ruleset.json"),
            """
            { "rules": [ { "type": "required_status_checks",
                           "parameters": { "required_status_checks": [ { "context": "outro-gate", "integration_id": 777 } ] } } ] }
            """);

        async Task<int> RunAsync(string stdin) =>
            (await ScriptShell.RunAsync($"{directory}/check-rules.sh", [], stdin, NoEnvironment)).ExitCode;

        var customCheck = "{\"context\":\"outro-gate\",\"integration_id\":777}";

        Assert.Equal(0, await RunAsync(FullResponse(customCheck)));   // os valores do arquivo novo
        Assert.Equal(1, await RunAsync(FullResponse(GoodCheck)));     // os valores do repositório deixam de valer
    }

    #endregion
}
