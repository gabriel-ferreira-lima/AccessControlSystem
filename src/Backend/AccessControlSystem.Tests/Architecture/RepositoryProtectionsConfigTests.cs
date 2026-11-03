using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AccessControlSystem.Tests.Support;

namespace AccessControlSystem.Tests.Architecture;

// FR-001 a FR-005a: as proteções da principal são configuração VERSIONADA. Esta classe trava o corpo do ruleset
// (scripts/github/protections/main-ruleset.json), que é a fonte única do nome do check exigido, do integration_id do
// aplicativo que o reporta e das regras. Nada aqui toca o GitHub: lê o arquivo. Contrato:
// specs/20261102-171227-delivery-foundation/contracts/repository-protections.md
public class RepositoryProtectionsConfigTests
{
    private const string RulesetPath = "scripts/github/protections/main-ruleset.json";
    private const string GateWorkflowPath = ".github/workflows/ci-gate.yml";

    // O id do aplicativo "GitHub Actions" (GET /apps/github-actions). Literal escrito aqui de propósito: se mudar por
    // acidente no JSON, o teste acusa; um valor errado travaria todos os pull requests.
    private const long GitHubActionsAppId = 15368;

    private static string FullPath(string relative) =>
        Path.Combine(ScriptShell.RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    private static JsonElement Ruleset()
    {
        var path = FullPath(RulesetPath);
        Assert.True(File.Exists(path), $"Arquivo esperado não existe: {RulesetPath}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private static JsonElement Rule(JsonElement ruleset, string type) =>
        ruleset.GetProperty("rules").EnumerateArray().Single(rule => rule.GetProperty("type").GetString() == type);

    #region Corpo do ruleset

    [Fact]
    public void Ruleset_tem_o_nome_o_alvo_e_a_aplicacao_ativa()
    {
        var ruleset = Ruleset();

        Assert.Equal("protect-main", ruleset.GetProperty("name").GetString());
        Assert.Equal("branch", ruleset.GetProperty("target").GetString());
        Assert.Equal("active", ruleset.GetProperty("enforcement").GetString());   // "evaluate" só existe no Enterprise
    }

    [Fact]
    public void Ruleset_nao_tem_nenhum_bypass_nem_para_admin()
    {
        var bypass = Ruleset().GetProperty("bypass_actors");

        Assert.Equal(JsonValueKind.Array, bypass.ValueKind);
        Assert.Equal(0, bypass.GetArrayLength());
    }

    [Fact]
    public void Ruleset_vale_so_para_a_main()
    {
        var refName = Ruleset().GetProperty("conditions").GetProperty("ref_name");

        Assert.Equal(new[] { "refs/heads/main" }, refName.GetProperty("include").EnumerateArray().Select(e => e.GetString()).ToArray());
        Assert.Equal(0, refName.GetProperty("exclude").GetArrayLength());
    }

    [Fact]
    public void Ruleset_tem_exatamente_as_quatro_regras_e_nenhuma_a_mais()
    {
        var types = Ruleset().GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("type").GetString()).Order().ToArray();

        Assert.Equal(new[] { "deletion", "non_fast_forward", "pull_request", "required_status_checks" }, types);
    }

    [Fact]
    public void Pull_request_e_obrigatorio_sem_revisao_humana_com_os_cinco_parametros_explicitos()
    {
        var parameters = Rule(Ruleset(), "pull_request").GetProperty("parameters");

        // Os cinco parâmetros são obrigatórios no esquema do GitHub, por isso aparecem todos, e só eles.
        Assert.Equal(
            new[]
            {
                "dismiss_stale_reviews_on_push", "require_code_owner_review", "require_last_push_approval",
                "required_approving_review_count", "required_review_thread_resolution",
            },
            parameters.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());

        Assert.Equal(0, parameters.GetProperty("required_approving_review_count").GetInt32());   // dono único: sem aprovação de outra pessoa
        foreach (var flag in new[] { "dismiss_stale_reviews_on_push", "require_code_owner_review", "require_last_push_approval", "required_review_thread_resolution" })
            Assert.Equal(JsonValueKind.False, parameters.GetProperty(flag).ValueKind);
    }

    [Fact]
    public void O_unico_check_exigido_e_o_ci_gate_preso_ao_aplicativo_GitHub_Actions()
    {
        var parameters = Rule(Ruleset(), "required_status_checks").GetProperty("parameters");

        var checks = parameters.GetProperty("required_status_checks").EnumerateArray().ToArray();
        var gate = Assert.Single(checks);

        Assert.Equal("ci-gate", gate.GetProperty("context").GetString());

        // Presente, INTEIRO e igual ao id do GitHub Actions: sem ele um status externo de mesmo nome valeria.
        var integrationId = gate.GetProperty("integration_id");
        Assert.Equal(JsonValueKind.Number, integrationId.ValueKind);
        Assert.Equal(GitHubActionsAppId, integrationId.GetInt64());

        // Não exige a branch atualizada com a main (decisão do clarify; a rede de segurança é o pós-merge).
        Assert.Equal(JsonValueKind.False, parameters.GetProperty("strict_required_status_checks_policy").ValueKind);
    }

    [Fact]
    public void O_context_exigido_e_igual_ao_nome_do_job_final_do_ci_gate_yml()
    {
        // Renomear o job ci-gate derruba a exigência sem aviso: este teste prende os dois nomes um ao outro.
        var context = Rule(Ruleset(), "required_status_checks").GetProperty("parameters")
            .GetProperty("required_status_checks")[0].GetProperty("context").GetString();

        Assert.Equal(context, GateJobName());
    }

    /// <summary>O <c>name:</c> do job <c>ci-gate</c> em <c>ci-gate.yml</c> (o texto exibido, que o ruleset exige).</summary>
    private static string GateJobName()
    {
        var path = FullPath(GateWorkflowPath);
        Assert.True(File.Exists(path), $"Arquivo esperado não existe: {GateWorkflowPath}");

        var lines = File.ReadAllText(path).Replace("\r\n", "\n").Split('\n');
        var jobs = Array.FindIndex(lines, l => l.TrimEnd() == "jobs:");
        Assert.True(jobs >= 0, "ci-gate.yml sem a seção jobs:.");

        var start = Array.FindIndex(lines, jobs, l => l.TrimEnd() == "  ci-gate:");
        Assert.True(start >= 0, "ci-gate.yml sem o job com id 'ci-gate'.");

        for (var i = start + 1; i < lines.Length; i++)
        {
            if (Regex.IsMatch(lines[i], @"^( {2}\S|\S)")) break;   // próximo job ou próxima seção

            var match = Regex.Match(lines[i], @"^ {4}name:\s*(?<name>.+?)\s*$");
            if (match.Success) return match.Groups["name"].Value.Trim('"', '\'');
        }

        throw new Xunit.Sdk.XunitException("O job ci-gate não tem 'name:'.");
    }

    #endregion

    // ---- Fase D: os scripts que aplicam e conferem as proteções, e o guia ----
    // Os scripts chamam "$GH_BIN" (padrão: o gh de verdade). TODO teste passa o GH_BIN do FakeGh, que só registra as chamadas e
    // devolve respostas enlatadas: nada aqui toca o GitHub. O --dry-run real e a aplicação real são do dono (quickstart V-07).

    #region Scripts: arquivos

    private const string Apply = "scripts/github/apply-protections.sh";
    private const string Manual = "scripts/github/check-protections-manual.sh";

    [Theory]
    [InlineData(Apply)]
    [InlineData(Manual)]
    public void Os_scripts_de_protecao_existem_em_LF_com_set_euo_pipefail_e_usam_o_GH_BIN(string script)
    {
        var path = FullPath(script);
        Assert.True(File.Exists(path), $"Arquivo esperado não existe: {script}");

        var text = File.ReadAllText(path);
        Assert.DoesNotContain("\r", text);                        // LF: o bash não aceita CR no fim das linhas
        Assert.StartsWith("#!/usr/bin/env bash", text);
        Assert.Contains("set -euo pipefail", text);
        Assert.Contains("GH_BIN=\"${GH_BIN:-gh}\"", text);        // o padrão é o gh; só os testes o trocam
    }

    #endregion

    #region Respostas enlatadas do gh falso

    // Os padrões são globs do bash sobre os argumentos juntos por espaço ({owner} e {repo} são trocados pelo gh de verdade).
    private const string CheckRunsGet = "api repos/{owner}/{repo}/commits/main/check-runs?check_name=ci-gate";
    private const string RulesetsGet = "api repos/{owner}/{repo}/rulesets";
    private const string RepoGet = "api repos/{owner}/{repo}";
    private const string RulesGet = "api repos/{owner}/{repo}/rules/branches/main?per_page=100";
    private const string RulesetPost = "api -X POST repos/{owner}/{repo}/rulesets --input -";
    private const string RulesetPutAny = "api -X PUT repos/{owner}/{repo}/rulesets/* --input -";
    private const string RepoPatch = "api -X PATCH repos/{owner}/{repo} --input -";

    private static string RulesetPut(long id) => $"api -X PUT repos/{{owner}}/{{repo}}/rulesets/{id} --input -";

    private static string RulesetGet(long id) => $"api repos/{{owner}}/{{repo}}/rulesets/{id}";

    private static string CheckRuns(int total, long appId = GitHubActionsAppId) =>
        total == 0
            ? "{\"total_count\":0,\"check_runs\":[]}"
            : "{\"total_count\":" + total + ",\"check_runs\":[{\"name\":\"ci-gate\",\"status\":\"completed\",\"conclusion\":\"success\"," +
              "\"app\":{\"id\":" + appId + ",\"slug\":\"github-actions\"}}]}";

    private static string Rulesets(params (long Id, string Name)[] items) =>
        "[" + string.Join(",", items.Select(item =>
            "{\"id\":" + item.Id + ",\"name\":\"" + item.Name + "\",\"target\":\"branch\",\"enforcement\":\"active\"}")) + "]";

    private static string Repo(string secretScanning = "enabled", string pushProtection = "enabled", bool withBlock = true) =>
        withBlock
            ? "{\"full_name\":\"dono/repo\",\"security_and_analysis\":{\"secret_scanning\":{\"status\":\"" + secretScanning +
              "\"},\"secret_scanning_push_protection\":{\"status\":\"" + pushProtection + "\"}}}"
            : "{\"full_name\":\"dono/repo\"}";

    private static string RulesetDetail(string enforcement = "active", string bypassActors = "[]", string canBypass = "never") =>
        "{\"id\":1234,\"name\":\"protect-main\",\"enforcement\":\"" + enforcement + "\",\"bypass_actors\":" + bypassActors +
        ",\"current_user_can_bypass\":\"" + canBypass + "\"}";

    private const string Source =
        "\"ruleset_source_type\":\"Repository\",\"ruleset_source\":\"dono/repo\",\"ruleset_id\":1234";

    // O que o endpoint de regras devolve com o ruleset aplicado (as quatro regras, o check preso ao app do Actions).
    private const string GoodRules =
        "[{\"type\":\"pull_request\"," + Source + ",\"parameters\":{\"required_approving_review_count\":0," +
        "\"dismiss_stale_reviews_on_push\":false,\"require_code_owner_review\":false,\"require_last_push_approval\":false," +
        "\"required_review_thread_resolution\":false}}," +
        "{\"type\":\"required_status_checks\"," + Source + ",\"parameters\":{\"required_status_checks\":" +
        "[{\"context\":\"ci-gate\",\"integration_id\":15368}],\"strict_required_status_checks_policy\":false}}," +
        "{\"type\":\"non_fast_forward\"," + Source + "},{\"type\":\"deletion\"," + Source + "}]";

    /// <summary>O estado "normal" do repositório: gate na main vindo do Actions, sem ruleset ainda, varredura ligada.</summary>
    private static void ApplyDefaults(FakeGh gh) =>
        gh.Respond(CheckRunsGet, CheckRuns(1))
          .Respond(RulesetsGet, Rulesets())
          .Respond(RepoGet, Repo())
          .Respond(RulesetPost, "{\"id\":1234}")
          .Respond(RulesetPutAny, "{\"id\":1234}")
          .Respond(RepoPatch, "{}")
          .Respond(RulesGet, GoodRules);

    private static void ManualDefaults(FakeGh gh) =>
        gh.Respond(RulesetsGet, Rulesets((1234, "protect-main")))
          .Respond(RulesetGet(1234), RulesetDetail())
          .Respond(RepoGet, Repo());

    /// <summary>
    /// Roda o script com <c>GH_BIN</c> apontando SEMPRE para o gh falso. Os testes registram antes as respostas que mudam;
    /// as padrão entram por último e só valem para o que o teste não respondeu.
    /// </summary>
    private static async Task<ScriptResult> RunAsync(string script, FakeGh gh, Action<FakeGh> defaults, params string[] arguments)
    {
        defaults(gh);
        return await ScriptShell.RunAsync(script, arguments, null, new Dictionary<string, string?> { ["GH_BIN"] = gh.GhPath });
    }

    private static string Output(ScriptResult result) => result.StdOut + result.StdErr;

    private static int IndexOf(FakeGh gh, string call) => gh.Calls.ToList().IndexOf(call);

    #endregion

    #region apply-protections.sh

    [Fact]
    public async Task Dry_run_le_o_estado_imprime_o_corpo_e_nao_faz_nenhuma_chamada_de_escrita()
    {
        using var gh = new FakeGh();

        var result = await RunAsync(Apply, gh, ApplyDefaults, "--dry-run");

        Assert.True(result.ExitCode == 0, Output(result));
        Assert.Empty(gh.Writes);                                   // nenhum POST, PUT, PATCH nem DELETE
        Assert.Contains(CheckRunsGet, gh.Calls);                   // mas LÊ (confere a ordem de ativação)
        Assert.Contains("protect-main", Output(result));           // imprime o corpo que enviaria
        Assert.Contains("15368", Output(result));
        Assert.Contains("dry-run", Output(result));
    }

    [Theory]
    [InlineData(false, "POST")]
    [InlineData(true, "PUT")]
    public async Task Dry_run_diz_se_criaria_ou_atualizaria_o_ruleset(bool exists, string expected)
    {
        using var gh = new FakeGh();
        if (exists) gh.Respond(RulesetsGet, Rulesets((1234, "protect-main")));

        var result = await RunAsync(Apply, gh, ApplyDefaults, "--dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(gh.Writes);
        Assert.Matches($@"\[dry-run\].*{expected}", Output(result));
    }

    [Fact]
    public async Task Sem_o_check_run_ci_gate_na_main_falha_pela_ordem_de_ativacao_sem_escrever()
    {
        using var gh = new FakeGh().Respond(CheckRunsGet, CheckRuns(0));

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("ordem de ativação", Output(result));
        Assert.Empty(gh.Writes);
    }

    [Fact]
    public async Task Check_run_do_ci_gate_de_outro_aplicativo_falha_citando_o_integration_id_sem_escrever()
    {
        using var gh = new FakeGh().Respond(CheckRunsGet, CheckRuns(1, appId: 99999));

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("integration_id", Output(result));
        Assert.Contains("15368", Output(result));
        Assert.Contains("99999", Output(result));
        Assert.Empty(gh.Writes);
    }

    [Fact]
    public async Task Sem_o_ruleset_cria_com_um_unico_POST_com_o_corpo_do_main_ruleset_json()
    {
        using var gh = new FakeGh();

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.True(result.ExitCode == 0, Output(result));
        Assert.Equal(new[] { RulesetPost }, gh.Writes);            // um POST e mais nada (a varredura já estava ligada)

        var sent = JsonNode.Parse(gh.InputOf(IndexOf(gh, RulesetPost))!);
        var expected = JsonNode.Parse(File.ReadAllText(FullPath(RulesetPath)));
        Assert.True(JsonNode.DeepEquals(expected, sent), "O corpo enviado difere de main-ruleset.json.");
    }

    [Fact]
    public async Task Com_o_ruleset_existente_atualiza_com_um_PUT_no_mesmo_id_e_nunca_cria_dois()
    {
        using var gh = new FakeGh().Respond(RulesetsGet, Rulesets((55, "outro-ruleset"), (1234, "protect-main")));

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.True(result.ExitCode == 0, Output(result));
        Assert.Equal(new[] { RulesetPut(1234) }, gh.Writes);       // um PUT no id do protect-main; nenhum POST
        var sent = JsonNode.Parse(gh.InputOf(IndexOf(gh, RulesetPut(1234)))!);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(FullPath(RulesetPath))), sent));
    }

    [Fact]
    public async Task Varredura_desligada_envia_um_PATCH_com_as_duas_protecoes_ligadas_e_rele_para_confirmar()
    {
        using var gh = new FakeGh().RespondOnce(RepoGet, Repo(secretScanning: "disabled", pushProtection: "disabled"));

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.True(result.ExitCode == 0, Output(result));
        var calls = gh.Calls.ToList();
        var patch = calls.IndexOf(RepoPatch);
        Assert.True(patch >= 0, "Sem PATCH da varredura de segredos.");
        Assert.Single(calls, c => c == RepoPatch);

        var body = JsonNode.Parse(gh.InputOf(patch)!)!["security_and_analysis"]!;
        Assert.Equal("enabled", (string?)body["secret_scanning"]!["status"]);
        Assert.Equal("enabled", (string?)body["secret_scanning_push_protection"]!["status"]);

        // A RELEITURA: o GET do repositório vem antes E depois do PATCH.
        Assert.Contains(RepoGet, calls.Take(patch));
        Assert.Contains(RepoGet, calls.Skip(patch + 1));
    }

    [Fact]
    public async Task Com_so_a_protecao_de_envio_desligada_o_PATCH_tambem_acontece()
    {
        using var gh = new FakeGh().RespondOnce(RepoGet, Repo(secretScanning: "enabled", pushProtection: "disabled"));

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(RepoPatch, gh.Writes);
    }

    [Fact]
    public async Task Varredura_ja_ligada_nao_envia_nenhum_PATCH()
    {
        using var gh = new FakeGh();

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain(RepoPatch, gh.Calls);
    }

    [Fact]
    public async Task PATCH_recusado_falha_com_o_passo_manual_da_interface()
    {
        using var gh = new FakeGh()
            .RespondOnce(RepoGet, Repo(secretScanning: "disabled", pushProtection: "disabled"))
            .Respond(RepoPatch, "HTTP 422: Validation Failed", exitCode: 1);

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Settings", Output(result));
        Assert.Contains("Advanced Security", Output(result));
    }

    [Fact]
    public async Task PATCH_aceito_mas_a_releitura_ainda_desligada_falha()
    {
        using var gh = new FakeGh().Respond(RepoGet, Repo(secretScanning: "disabled", pushProtection: "disabled"));

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Advanced Security", Output(result));
    }

    [Fact]
    public async Task Sem_o_bloco_security_and_analysis_falha_antes_de_escrever_qualquer_coisa()
    {
        using var gh = new FakeGh().Respond(RepoGet, Repo(withBlock: false));   // sem permissão de administrador

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("administrador", Output(result));
        Assert.Empty(gh.Writes);
    }

    [Fact]
    public async Task A_conferencia_automatica_reprovada_no_fim_faz_o_script_falhar()
    {
        using var gh = new FakeGh().Respond(RulesGet, "[]");   // o ruleset foi enviado, mas a leitura não mostra as regras

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("falta:", Output(result));
    }

    [Fact]
    public async Task Aplicado_com_sucesso_imprime_os_passos_da_conferencia_a_mao()
    {
        using var gh = new FakeGh();

        var result = await RunAsync(Apply, gh, ApplyDefaults);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("check-protections-manual.sh", Output(result));
    }

    [Fact]
    public async Task Disable_envia_so_o_enforcement_disabled_e_nao_exige_o_gate()
    {
        using var gh = new FakeGh().Respond(RulesetsGet, Rulesets((1234, "protect-main")));

        var result = await RunAsync(Apply, gh, ApplyDefaults, "--disable");

        Assert.True(result.ExitCode == 0, Output(result));
        Assert.Equal(new[] { RulesetPut(1234) }, gh.Writes);
        Assert.DoesNotContain(CheckRunsGet, gh.Calls);              // emergência: não depende de o gate existir

        var body = JsonNode.Parse(gh.InputOf(IndexOf(gh, RulesetPut(1234)))!)!.AsObject();
        Assert.Equal(new[] { "enforcement" }, body.Select(p => p.Key).ToArray());   // SÓ o enforcement
        Assert.Equal("disabled", (string?)body["enforcement"]);
    }

    [Fact]
    public async Task Enable_envia_so_o_enforcement_active()
    {
        using var gh = new FakeGh().Respond(RulesetsGet, Rulesets((1234, "protect-main")));

        var result = await RunAsync(Apply, gh, ApplyDefaults, "--enable");

        Assert.True(result.ExitCode == 0, Output(result));
        Assert.Equal(new[] { RulesetPut(1234) }, gh.Writes);

        var body = JsonNode.Parse(gh.InputOf(IndexOf(gh, RulesetPut(1234)))!)!.AsObject();
        Assert.Equal(new[] { "enforcement" }, body.Select(p => p.Key).ToArray());
        Assert.Equal("active", (string?)body["enforcement"]);
    }

    [Theory]
    [InlineData("--disable")]
    [InlineData("--enable")]
    public async Task Disable_e_enable_sem_o_ruleset_falham_sem_escrever(string option)
    {
        using var gh = new FakeGh();   // a lista de rulesets vem vazia

        var result = await RunAsync(Apply, gh, ApplyDefaults, option);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("protect-main", Output(result));
        Assert.Empty(gh.Writes);
    }

    [Fact]
    public async Task Sem_o_gh_falha_citando_o_Git_Bash()
    {
        // GH_BIN aponta para um arquivo que não existe: o script não pode tentar nada.
        var result = await ScriptShell.RunAsync(Apply, [], null, new Dictionary<string, string?>
        {
            ["GH_BIN"] = $"{ScriptShell.NewScratchDirectory()}/gh-que-nao-existe",
        });

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Git Bash", Output(result));
    }

    [Theory]
    [InlineData("--nao-existe")]
    [InlineData("--dry-run --disable")]
    public async Task Opcao_desconhecida_ou_combinada_falha_com_o_uso_sem_chamar_o_gh(string options)
    {
        using var gh = new FakeGh();

        var result = await RunAsync(Apply, gh, ApplyDefaults, options.Split(' '));

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Uso", Output(result));
        Assert.Empty(gh.Calls);
    }

    #endregion

    #region check-protections-manual.sh

    [Fact]
    public async Task Conferencia_a_mao_com_tudo_certo_sai_com_0_e_uma_linha_OK_por_item()
    {
        using var gh = new FakeGh();

        var result = await RunAsync(Manual, gh, ManualDefaults);

        Assert.True(result.ExitCode == 0, Output(result));
        var lines = Output(result).Split('\n').Select(l => l.Trim()).ToArray();
        Assert.Equal(5, lines.Count(l => l.StartsWith("OK", StringComparison.Ordinal)));   // um por item
        Assert.DoesNotContain(lines, l => l.StartsWith("FALHA", StringComparison.Ordinal));
        Assert.Empty(gh.Writes);                                                              // só lê
    }

    [Theory]
    [InlineData("bypass_actors_nao_vazio", "active", "[{\"actor_id\":5,\"actor_type\":\"RepositoryRole\",\"bypass_mode\":\"always\"}]", "never", "enabled", "enabled", "bypass_actors")]
    [InlineData("pode_furar_sempre", "active", "[]", "always", "enabled", "enabled", "current_user_can_bypass")]
    [InlineData("pode_furar_em_pr", "active", "[]", "pull_requests_only", "enabled", "enabled", "current_user_can_bypass")]
    [InlineData("ruleset_desligado", "disabled", "[]", "never", "enabled", "enabled", "enforcement")]
    [InlineData("ruleset_em_avaliacao", "evaluate", "[]", "never", "enabled", "enabled", "enforcement")]
    [InlineData("varredura_desligada", "active", "[]", "never", "disabled", "enabled", "secret_scanning")]
    [InlineData("protecao_de_envio_desligada", "active", "[]", "never", "enabled", "disabled", "secret_scanning_push_protection")]
    public async Task Conferencia_a_mao_reprova_cada_item_fora_do_lugar(
        string caso, string enforcement, string bypassActors, string canBypass, string secretScanning, string pushProtection, string item)
    {
        using var gh = new FakeGh()
            .Respond(RulesetGet(1234), RulesetDetail(enforcement, bypassActors, canBypass))
            .Respond(RepoGet, Repo(secretScanning, pushProtection));

        var result = await RunAsync(Manual, gh, ManualDefaults);

        Assert.True(result.ExitCode == 1, $"[{caso}] {Output(result)}");
        var failures = Output(result).Split('\n').Where(l => l.TrimStart().StartsWith("FALHA", StringComparison.Ordinal)).ToArray();
        Assert.Single(failures);                       // só o item fora do lugar
        Assert.Contains(item, failures[0]);
    }

    [Fact]
    public async Task Conferencia_a_mao_sem_o_bloco_security_and_analysis_reprova_com_mensagem_sobre_administrador()
    {
        using var gh = new FakeGh().Respond(RepoGet, Repo(withBlock: false));

        var result = await RunAsync(Manual, gh, ManualDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("administrador", Output(result));
    }

    [Fact]
    public async Task Conferencia_a_mao_sem_o_ruleset_reprova()
    {
        using var gh = new FakeGh().Respond(RulesetsGet, Rulesets());

        var result = await RunAsync(Manual, gh, ManualDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("protect-main", Output(result));
    }

    [Fact]
    public async Task Conferencia_a_mao_sem_conseguir_ler_o_bypass_reprova()
    {
        // Quem não é administrador não vê bypass_actors: não conseguir ler nunca é aprovar.
        using var gh = new FakeGh().Respond(RulesetGet(1234), "{\"id\":1234,\"name\":\"protect-main\",\"enforcement\":\"active\"}");

        var result = await RunAsync(Manual, gh, ManualDefaults);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("FALHA", Output(result));
    }

    #endregion

    #region O guia

    private const string GuidePath = "docs/protecoes-do-repositorio.md";

    private static readonly string[] GuideSections =
    [
        "O que está protegido e por quê",
        "Como aplicar e refazer",
        "Conferência automática e conferência à mão",
        "Ordem de ativação",
        "Emergência: desligar e religar",
        "Ensaios das proteções",
        "Se o repositório voltar a ser privado",
        "Onde ver os resultados",
        "Agendamentos desativados e avisos por e-mail",
        "Rodar no Windows (Git Bash, ACS_BASH, jq)",
    ];

    private static string Guide()
    {
        var path = FullPath(GuidePath);
        Assert.True(File.Exists(path), $"Arquivo esperado não existe: {GuidePath}");
        return File.ReadAllText(path).Replace("\r\n", "\n");
    }

    [Fact]
    public void O_guia_tem_exatamente_as_dez_secoes_na_ordem_prevista()
    {
        var titles = Guide().Split('\n')
            .Where(line => line.StartsWith("## ", StringComparison.Ordinal))
            .Select(line => line[3..].Trim())
            .ToArray();

        Assert.Equal(GuideSections, titles);
    }

    [Theory]
    [InlineData("ci-gate")]
    [InlineData("integration_id")]
    [InlineData("SHA")]
    [InlineData("ACS_BASH")]
    [InlineData("conflito")]                // PR com conflito de merge: o gate nem inicia (U3)
    [InlineData("primeiro contribuinte")]   // PR de fork de primeiro contribuinte: espera a aprovação do dono (U3)
    [InlineData("Git Bash")]
    [InlineData("--dry-run")]
    [InlineData("--disable")]
    [InlineData("--enable")]
    [InlineData("check-protections-manual.sh")]
    [InlineData("protections-check")]
    [InlineData("package-audit")]
    public void O_guia_menciona(string term)
    {
        Assert.Contains(term, Guide());
    }

    #endregion
}
