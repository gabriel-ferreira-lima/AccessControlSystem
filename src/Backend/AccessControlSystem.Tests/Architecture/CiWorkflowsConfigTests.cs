using System.Text.RegularExpressions;
using AccessControlSystem.Tests.Support;

namespace AccessControlSystem.Tests.Architecture;

// Os workflows são configuração versionada; este teste trava as garantias que o gate e as verificações agendadas
// dependem (FR-006 a FR-018, FR-037a): gatilhos sem filtro de caminhos, permissões mínimas, tempo limite em todo job,
// nenhum segredo, nenhuma ação de terceiros, e os nomes dos jobs que o ruleset e os resumos citam.
// Sem biblioteca de YAML (mesmo estilo do PackageAuditConfigTests): lê as linhas e separa os jobs pela indentação.
// Falha, não pula, se um arquivo esperado não existir. Contratos: contracts/ci-gate.md e contracts/scheduled-workflows.md
public class CiWorkflowsConfigTests
{
    private const string WorkflowsDirectory = ".github/workflows";

    // O nome exibido do passo k6 reservado. A spec 06 o troca (para "k6 curto") junto com esta asserção.
    private const string K6JobName = "k6 curto (não ativado)";

    #region Leitura e separação em blocos

    private static string FullPath(string relative) =>
        Path.Combine(ScriptShell.RepositoryRoot(), relative.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Todo workflow do repositório: qualquer arquivo novo entra nas regras gerais automaticamente.</summary>
    public static TheoryData<string> AllWorkflows()
    {
        var data = new TheoryData<string>();
        var directory = FullPath(WorkflowsDirectory);
        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.yml").Order(StringComparer.Ordinal))
                data.Add(Path.GetFileName(file));
        }
        return data;
    }

    /// <summary>Linhas do workflow SEM os comentários de linha inteira (o texto de um comentário não vale como configuração).</summary>
    private static string[] Lines(string workflow)
    {
        var path = FullPath($"{WorkflowsDirectory}/{workflow}");
        Assert.True(File.Exists(path), $"Arquivo esperado não existe: {WorkflowsDirectory}/{workflow}");

        return File.ReadAllText(path).Replace("\r\n", "\n").Split('\n')
            .Where(line => !line.TrimStart().StartsWith('#'))
            .ToArray();
    }

    /// <summary>Linhas (sem a do nome) da chave de topo <paramref name="key"/> (coluna 0) até a próxima chave de topo.</summary>
    private static string[] TopBlock(string[] lines, string key)
    {
        var start = Array.FindIndex(lines, l => l.TrimEnd() == $"{key}:" || l.StartsWith($"{key}: ", StringComparison.Ordinal));
        if (start < 0) return [];

        var block = new List<string>();
        for (var i = start + 1; i < lines.Length; i++)
        {
            if (lines[i].Length > 0 && !char.IsWhiteSpace(lines[i][0])) break;
            block.Add(lines[i]);
        }
        return block.ToArray();
    }

    /// <summary>Gatilhos do workflow: as chaves com 2 espaços sob <c>on:</c>.</summary>
    private static string[] Triggers(string[] lines) =>
        TopBlock(lines, "on")
            .Select(l => Regex.Match(l, @"^ {2}(?<key>[A-Za-z_]+):"))
            .Where(m => m.Success)
            .Select(m => m.Groups["key"].Value)
            .Order(StringComparer.Ordinal)
            .ToArray();

    /// <summary>Linhas do gatilho <paramref name="trigger"/> (as de indentação maior que a da chave dele).</summary>
    private static string[] TriggerBlock(string[] lines, string trigger)
    {
        var on = TopBlock(lines, "on");
        var start = Array.FindIndex(on, l => Regex.IsMatch(l, $@"^ {{2}}{trigger}:"));
        if (start < 0) return [];

        return on.Skip(start + 1).TakeWhile(l => !Regex.IsMatch(l, @"^ {2}\S")).ToArray();
    }

    /// <summary>Jobs sob <c>jobs:</c>, por id (chave com 2 espaços), com as linhas de cada um.</summary>
    private static Dictionary<string, string[]> Jobs(string[] lines)
    {
        var block = TopBlock(lines, "jobs");
        var jobs = new Dictionary<string, string[]>();

        for (var i = 0; i < block.Length; i++)
        {
            var match = Regex.Match(block[i], @"^ {2}(?<id>[A-Za-z0-9_-]+):\s*$");
            if (!match.Success) continue;

            var body = block.Skip(i + 1).TakeWhile(l => !Regex.IsMatch(l, @"^ {2}\S")).ToArray();
            jobs[match.Groups["id"].Value] = body;
        }
        return jobs;
    }

    /// <summary>Valor de uma chave do PRÓPRIO job (4 espaços de indentação); <c>null</c> se não existir.</summary>
    private static string? JobValue(string[] job, string key)
    {
        foreach (var line in job)
        {
            var match = Regex.Match(line, $@"^ {{4}}{Regex.Escape(key)}:\s*(?<value>.*?)\s*$");
            if (match.Success) return match.Groups["value"].Value;
        }
        return null;
    }

    /// <summary>Os jobs listados em <c>needs:</c> (lista em linha <c>[a, b]</c> ou em itens <c>- a</c>).</summary>
    private static string[] Needs(string[] job)
    {
        var at = Array.FindIndex(job, l => Regex.IsMatch(l, @"^ {4}needs:"));
        if (at < 0) return [];

        var inline = JobValue(job, "needs")!;
        if (inline.StartsWith('['))
            return inline.Trim('[', ']').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Order(StringComparer.Ordinal).ToArray();
        if (inline.Length > 0)
            return [inline];

        return job.Skip(at + 1)
            .TakeWhile(l => Regex.IsMatch(l, @"^ {6}- "))
            .Select(l => l.Trim().TrimStart('-').Trim())
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string Unquote(string? value) => (value ?? string.Empty).Trim('"', '\'');

    #endregion

    #region Regras gerais (todo workflow do repositório)

    [Theory]
    [MemberData(nameof(AllWorkflows))]
    public void Todo_workflow_declara_permissions_contents_read_no_topo(string workflow)
    {
        var permissions = TopBlock(Lines(workflow), "permissions").Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

        Assert.Equal(new[] { "contents: read" }, permissions);   // só leitura do conteúdo, e nada mais
    }

    [Theory]
    [MemberData(nameof(AllWorkflows))]
    public void Nenhum_workflow_usa_pull_request_target_segredo_ou_acao_de_terceiros(string workflow)
    {
        var lines = Lines(workflow);

        Assert.DoesNotContain(lines, l => l.Contains("pull_request_target", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, l => l.Contains("secrets.", StringComparison.Ordinal));

        var uses = lines
            .Select(l => Regex.Match(l, @"^\s*(?:-\s*)?uses:\s*(?<value>\S+)"))
            .Where(m => m.Success)
            .Select(m => m.Groups["value"].Value)
            .ToArray();

        Assert.All(uses, value => Assert.True(
            value.StartsWith("actions/", StringComparison.Ordinal) || value.StartsWith("./", StringComparison.Ordinal),
            $"{workflow}: 'uses: {value}' não é de actions/ nem local (nenhuma ação de terceiros)."));
    }

    [Theory]
    [MemberData(nameof(AllWorkflows))]
    public void Todo_job_tem_tempo_limite_ou_delega_a_um_reusable_que_tem(string workflow)
    {
        foreach (var (id, job) in Jobs(Lines(workflow)))
        {
            var uses = JobValue(job, "uses");
            if (uses is null)
            {
                Assert.True(JobValue(job, "timeout-minutes") is not null, $"{workflow}: o job '{id}' não tem timeout-minutes.");
                continue;
            }

            // Job que chama um reusable workflow: o tempo limite fica nos jobs do arquivo chamado.
            Assert.StartsWith("./", uses);
            var called = uses[2..];
            foreach (var (calledId, calledJob) in Jobs(Lines(Path.GetFileName(called))))
                Assert.True(JobValue(calledJob, "timeout-minutes") is not null, $"{called}: o job '{calledId}' não tem timeout-minutes.");
        }
    }

    #endregion

    #region ci-gate.yml

    private const string Gate = "ci-gate.yml";

    [Fact]
    public void Ci_gate_roda_em_todo_pull_request_e_em_todo_push_na_main_sem_filtro_de_caminhos()
    {
        var lines = Lines(Gate);

        Assert.Equal(new[] { "pull_request", "push" }, Triggers(lines));   // e nada mais: sem workflow_dispatch
        foreach (var trigger in new[] { "pull_request", "push" })
        {
            var block = TriggerBlock(lines, trigger);
            Assert.Contains(block, l => Regex.IsMatch(l, @"^\s+(- )?main\s*$|branches:\s*\[\s*main\s*\]"));
            Assert.DoesNotContain(block, l => l.Contains("paths", StringComparison.Ordinal));   // nem paths nem paths-ignore
        }

        Assert.DoesNotContain(lines, l => l.Contains("workflow_dispatch", StringComparison.Ordinal));   // execução manual não satisfaz check exigido
    }

    [Fact]
    public void Ci_gate_tem_exatamente_os_cinco_jobs_do_contrato()
    {
        Assert.Equal(
            new[] { "backend", "changes", "ci-gate", "frontend", "k6-short" },
            Jobs(Lines(Gate)).Keys.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void O_job_final_se_chama_ci_gate_nunca_e_pulado_e_espera_os_quatro_jobs()
    {
        var job = Jobs(Lines(Gate))["ci-gate"];

        Assert.Equal("ci-gate", Unquote(JobValue(job, "name")));   // é o context exigido pelo ruleset
        Assert.Equal("always()", JobValue(job, "if"));              // sem isto o GitHub pularia o job quando um needs falha
        Assert.Equal(new[] { "backend", "changes", "frontend", "k6-short" }, Needs(job));
        Assert.Equal("3", JobValue(job, "timeout-minutes"));
    }

    [Fact]
    public void O_job_final_avalia_o_resultado_real_pelo_script_com_o_needs_inteiro()
    {
        var job = Jobs(Lines(Gate))["ci-gate"];

        Assert.Contains(job, l => l.Contains("scripts/ci/evaluate-gate.sh", StringComparison.Ordinal));
        Assert.Contains(job, l => l.Trim() == "NEEDS_JSON: ${{ toJSON(needs) }}");
        foreach (var variable in new[] { "AREA_BACKEND", "AREA_FRONTEND", "AREA_DOCS_ONLY" })
            Assert.Contains(job, l => l.TrimStart().StartsWith($"{variable}:", StringComparison.Ordinal));
    }

    [Fact]
    public void O_passo_k6_curto_aparece_como_nao_ativado_e_nao_roda_k6_de_verdade()
    {
        var job = Jobs(Lines(Gate))["k6-short"];

        Assert.Equal(K6JobName, Unquote(JobValue(job, "name")));   // é o texto que a lista de checks do PR mostra
        Assert.Equal("2", JobValue(job, "timeout-minutes"));
        Assert.DoesNotContain(job, l => l.Contains("k6 run", StringComparison.Ordinal));
        Assert.DoesNotContain(job, l => l.Contains("grafana/", StringComparison.Ordinal));
        Assert.Contains(job, l => l.Contains("NÃO ATIVADO", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("backend", "backend-ci.yml")]
    [InlineData("frontend", "frontend-ci.yml")]
    public void Backend_e_frontend_chamam_o_workflow_existente_so_na_area_aplicavel(string id, string called)
    {
        var job = Jobs(Lines(Gate))[id];

        Assert.Equal($"./.github/workflows/{called}", JobValue(job, "uses"));
        Assert.Equal(new[] { "changes" }, Needs(job));
        Assert.Equal($"needs.changes.outputs.{id} == 'true'", JobValue(job, "if"));
    }

    [Fact]
    public void O_job_changes_calcula_as_areas_pelo_script_e_falha_se_o_git_diff_falhar()
    {
        var job = Jobs(Lines(Gate))["changes"];

        Assert.Equal("3", JobValue(job, "timeout-minutes"));
        Assert.Contains(job, l => l.Contains("scripts/ci/detect-areas.sh", StringComparison.Ordinal));
        Assert.Contains(job, l => l.Contains("fetch-depth", StringComparison.Ordinal));   // o diff precisa do histórico
        Assert.Contains(job, l => l.Contains("diff --name-only", StringComparison.Ordinal));
        Assert.Contains(job, l => l.Contains("core.quotepath=false", StringComparison.Ordinal));   // nome com acento não vira texto entre aspas
        Assert.Contains(job, l => l.Contains("set -euo pipefail", StringComparison.Ordinal));   // diff que falha derruba o job: o gate reprova
        Assert.Contains(job, l => l.Contains("GITHUB_OUTPUT", StringComparison.Ordinal));
    }

    [Fact]
    public void A_concorrencia_cancela_a_execucao_antiga_so_em_pull_request()
    {
        var concurrency = TopBlock(Lines(Gate), "concurrency").Select(l => l.Trim()).ToArray();

        Assert.Contains(concurrency, l => l.StartsWith("group: ci-gate-", StringComparison.Ordinal));
        Assert.Contains("cancel-in-progress: ${{ github.event_name == 'pull_request' }}", concurrency);
    }

    #endregion

    #region backend-ci.yml e frontend-ci.yml (workflows chamados)

    [Theory]
    [InlineData("backend-ci.yml", "build-and-test", "15")]
    [InlineData("frontend-ci.yml", "frontend-build-and-test", "10")]
    public void Os_workflows_chamados_nao_tem_filtro_de_caminhos_e_conservam_o_job_com_tempo_limite(string workflow, string jobId, string timeout)
    {
        var lines = Lines(workflow);

        Assert.Equal(new[] { "workflow_call", "workflow_dispatch" }, Triggers(lines));   // sem push nem pull_request
        Assert.DoesNotContain(TopBlock(lines, "on"), l => l.Contains("paths", StringComparison.Ordinal));
        Assert.Equal(new[] { jobId }, Jobs(lines).Keys.ToArray());   // o nome do job não mudou
        Assert.Equal(timeout, JobValue(Jobs(lines)[jobId], "timeout-minutes"));
    }

    #endregion

    #region package-audit.yml

    private const string Audit = "package-audit.yml";

    [Fact]
    public void A_auditoria_roda_toda_semana_e_sob_demanda_e_nunca_por_pull_request()
    {
        var lines = Lines(Audit);

        Assert.Equal(new[] { "schedule", "workflow_dispatch" }, Triggers(lines));
        Assert.Contains(TriggerBlock(lines, "schedule"), l => Regex.IsMatch(l, @"^\s+- cron:\s*'[^']+'\s*$"));
        Assert.DoesNotContain(lines, l => l.Contains("pull_request", StringComparison.Ordinal));
    }

    [Fact]
    public void A_auditoria_tem_os_dois_jobs_com_tempo_limite_de_10_minutos()
    {
        var jobs = Jobs(Lines(Audit));

        Assert.Equal(new[] { "backend-audit", "frontend-audit" }, jobs.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.All(jobs.Values, job => Assert.Equal("10", JobValue(job, "timeout-minutes")));
    }

    [Fact]
    public void A_auditoria_usa_os_mesmos_comandos_e_criterios_da_04b_e_nada_alem_disso()
    {
        var lines = Lines(Audit).Select(l => l.Trim()).ToList();

        Assert.Contains("run: dotnet restore Backend.slnx", lines);
        Assert.Contains("run: npm ci", lines);
        Assert.Contains("run: npm audit", lines);   // exatamente: sem --audit-level (qualquer severidade)
        Assert.DoesNotContain(lines, l => l.Contains("--audit-level", StringComparison.Ordinal));
        Assert.True(lines.IndexOf("run: npm ci") < lines.IndexOf("run: npm audit"), "O npm audit precisa vir depois do npm ci.");

        // Falha de teste, de build ou de publicação não pode virar alarme de vulnerabilidade.
        foreach (var forbidden in new[] { "dotnet build", "dotnet test", "dotnet publish", "npm test", "npm run build", "npm run typecheck" })
            Assert.DoesNotContain(lines, l => l.Contains(forbidden, StringComparison.Ordinal));
    }

    #endregion

    #region protections-check.yml

    private const string Check = "protections-check.yml";

    [Fact]
    public void A_conferencia_roda_toda_semana_e_sob_demanda_e_nunca_por_pull_request()
    {
        var lines = Lines(Check);

        Assert.Equal(new[] { "schedule", "workflow_dispatch" }, Triggers(lines));
        Assert.Contains(TriggerBlock(lines, "schedule"), l => Regex.IsMatch(l, @"^\s+- cron:\s*'[^']+'\s*$"));
        Assert.DoesNotContain(lines, l => l.Contains("pull_request", StringComparison.Ordinal));
    }

    [Fact]
    public void A_conferencia_le_as_regras_autenticada_com_o_token_da_execucao_e_chama_o_script()
    {
        var lines = Lines(Check).Select(l => l.Trim()).ToList();
        var job = Jobs(Lines(Check))["check"];

        Assert.Equal("5", JobValue(job, "timeout-minutes"));
        Assert.Contains("GH_TOKEN: ${{ github.token }}", lines);   // autenticada, nunca anônima (o limite anônimo é por IP)
        Assert.Contains(lines, l => l.Contains("rules/branches/main?per_page=100", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("X-GitHub-Api-Version: 2022-11-28", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("scripts/github/check-rules.sh", StringComparison.Ordinal));
    }

    #endregion
}
