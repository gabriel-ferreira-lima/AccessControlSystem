using System.Text.Json;
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
}
