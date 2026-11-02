using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate;
using AccessControlSystem.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Authenticate;

// FR-017 a FR-022 e FR-032: limite de login por conta (tenant + e-mail). Cada tentativa é ADMITIDA de forma atômica ANTES
// da verificação da senha e conta como falha provisória; a 5ª já bloqueia a conta, sem impedir a própria 5ª. Espera de
// 1, 2, 4, 8 e 15 min (teto). Registro descartado depois de 15 min sem tentativa nem bloqueio. Saturado, falha fechada.
// Testes rápidos, sem Docker; o relógio é falso, então ninguém espera de verdade.
public class InMemoryLoginThrottleTests
{
    private static readonly Guid Tenant = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid OtherTenant = Guid.Parse("c2c2c2c2-0000-4000-8000-000000000002");
    private const string Email = "ana@test.com";

    private readonly FakeTimeProvider _clock = new();

    private InMemoryLoginThrottle Create(LoginThrottleOptions? options = null) =>
        new(_clock, Options.Create(options ?? new LoginThrottleOptions()), NullLogger<InMemoryLoginThrottle>.Instance);

    private static Task<TimeSpan?> TryAsync(InMemoryLoginThrottle throttle, string email = Email, Guid? tenant = null) =>
        throttle.TryAcquireAttemptAsync(tenant ?? Tenant, email);

    /// <summary>Faz <paramref name="count"/> tentativas e exige que todas sigam (<c>null</c>).</summary>
    private static async Task AttemptsAdmittedAsync(InMemoryLoginThrottle throttle, int count, string email = Email, Guid? tenant = null)
    {
        for (var i = 0; i < count; i++)
            Assert.Null(await TryAsync(throttle, email, tenant));
    }

    private static Dictionary<string, object?> Entries(InMemoryLoginThrottle throttle)
    {
        var field = typeof(InMemoryLoginThrottle)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(f => f.FieldType.IsGenericType
                         && f.FieldType.GetGenericTypeDefinition() == typeof(Dictionary<,>)
                         && f.FieldType.GetGenericArguments()[0] == typeof(string));

        var dictionary = (IDictionary)field.GetValue(throttle)!;
        return dictionary.Keys.Cast<string>().ToDictionary(k => k, k => (object?)dictionary[k]);
    }

    #region Contagem e bloqueio

    [Fact]
    public async Task As_cinco_primeiras_tentativas_seguem_e_a_sexta_e_bloqueada()
    {
        var throttle = Create();

        await AttemptsAdmittedAsync(throttle, 5);

        Assert.Equal(TimeSpan.FromMinutes(1), await TryAsync(throttle));
    }

    [Fact]
    public async Task A_quinta_tentativa_ja_bloqueia_a_proxima_sem_impedir_a_propria_quinta()
    {
        var throttle = Create();
        await AttemptsAdmittedAsync(throttle, 4);

        var quinta = await TryAsync(throttle);   // atinge o máximo: bloqueia, mas ESTA tentativa segue
        var sexta = await TryAsync(throttle);

        Assert.Null(quinta);
        Assert.NotNull(sexta);
        Assert.True(sexta > TimeSpan.Zero);
    }

    [Fact]
    public async Task Escada_de_bloqueio_e_1_2_4_8_e_15_minutos()
    {
        var throttle = Create();

        // O 5º bloqueio seria de 16 min e é limitado a 15; o 6º também.
        foreach (var segundos in new[] { 60, 120, 240, 480, 900, 900 })
        {
            await AttemptsAdmittedAsync(throttle, 5);

            var espera = await TryAsync(throttle);

            Assert.Equal(TimeSpan.FromSeconds(segundos), espera);
            _clock.Advance(espera!.Value);   // a espera passa e a conta volta
        }
    }

    [Fact]
    public async Task Durante_o_bloqueio_nenhuma_tentativa_e_contada()
    {
        var throttle = Create();
        await AttemptsAdmittedAsync(throttle, 5);
        for (var i = 0; i < 10; i++) Assert.NotNull(await TryAsync(throttle));   // 10 tentativas barradas

        _clock.Advance(TimeSpan.FromMinutes(1));

        // Se as barradas contassem, o próximo bloqueio chegaria antes de 5 tentativas.
        await AttemptsAdmittedAsync(throttle, 5);
        Assert.Equal(TimeSpan.FromMinutes(2), await TryAsync(throttle));
    }

    [Fact]
    public async Task Reset_zera_contagem_nivel_e_bloqueio()
    {
        var throttle = Create();

        // Depois de 3 tentativas: a contagem volta a zero.
        await AttemptsAdmittedAsync(throttle, 3);
        await throttle.ResetAsync(Tenant, Email);
        await AttemptsAdmittedAsync(throttle, 5);
        Assert.Equal(TimeSpan.FromMinutes(1), await TryAsync(throttle));
        await throttle.ResetAsync(Tenant, Email);

        // Depois do nível 3: o próximo bloqueio recomeça em 1 min.
        foreach (var _ in Enumerable.Range(0, 3))
        {
            await AttemptsAdmittedAsync(throttle, 5);
            _clock.Advance((await TryAsync(throttle))!.Value);
        }

        await throttle.ResetAsync(Tenant, Email);
        await AttemptsAdmittedAsync(throttle, 5);
        Assert.Equal(TimeSpan.FromMinutes(1), await TryAsync(throttle));

        // Com o bloqueio ativo: o login com sucesso libera a conta na hora.
        await throttle.ResetAsync(Tenant, Email);
        Assert.Null(await TryAsync(throttle));
    }

    #endregion

    #region Descarte (FR-032)

    [Fact]
    public async Task Registro_e_descartado_15_minutos_depois_da_ultima_tentativa()
    {
        var throttle = Create();
        await AttemptsAdmittedAsync(throttle, 4);

        _clock.Advance(TimeSpan.FromMinutes(15));

        // Contagem recomeça: as 5 seguintes passam e só a 6ª é bloqueada, de novo por 1 min.
        await AttemptsAdmittedAsync(throttle, 5);
        Assert.Equal(TimeSpan.FromMinutes(1), await TryAsync(throttle));
    }

    [Fact]
    public async Task Registro_e_descartado_15_minutos_depois_do_fim_do_bloqueio()
    {
        var throttle = Create();
        await AttemptsAdmittedAsync(throttle, 5);
        Assert.Equal(TimeSpan.FromMinutes(1), await TryAsync(throttle));

        _clock.Advance(TimeSpan.FromMinutes(1));    // fim do bloqueio
        _clock.Advance(TimeSpan.FromMinutes(15));   // + 15 min sem nenhuma tentativa

        // A espera recomeça em 1 min (e não em 2, o próximo degrau da escada).
        await AttemptsAdmittedAsync(throttle, 5);
        Assert.Equal(TimeSpan.FromMinutes(1), await TryAsync(throttle));
    }

    [Fact]
    public async Task Antes_de_15_minutos_o_registro_e_mantido()
    {
        var throttle = Create();
        await AttemptsAdmittedAsync(throttle, 4);

        _clock.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));

        Assert.Null(await TryAsync(throttle));        // a 5ª: ainda conta junto das 4 anteriores
        Assert.NotNull(await TryAsync(throttle));     // e a conta já está bloqueada
    }

    #endregion

    #region Isolamento e privacidade

    [Fact]
    public async Task Contas_de_tenants_diferentes_com_o_mesmo_email_sao_independentes()
    {
        var throttle = Create();
        await AttemptsAdmittedAsync(throttle, 5, Email, Tenant);
        Assert.NotNull(await TryAsync(throttle, Email, Tenant));   // bloqueada no tenant A

        await AttemptsAdmittedAsync(throttle, 5, Email, OtherTenant);   // o mesmo e-mail no tenant B segue livre
    }

    [Fact]
    public async Task Emails_diferentes_do_mesmo_tenant_sao_independentes()
    {
        var throttle = Create();
        await AttemptsAdmittedAsync(throttle, 5, "ana@test.com");
        Assert.NotNull(await TryAsync(throttle, "ana@test.com"));

        await AttemptsAdmittedAsync(throttle, 5, "bia@test.com");
    }

    [Fact]
    public async Task Chaves_do_registro_nao_guardam_o_email_nem_o_tenant_em_texto()
    {
        var throttle = Create();
        await TryAsync(throttle, "segredo.pessoal@test.com", Tenant);

        var keys = Entries(throttle).Keys.ToArray();

        Assert.Single(keys);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), keys[0]);
        Assert.DoesNotContain("segredo", keys[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Tenant.ToString("N")[..8], keys[0], StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Concorrência (FR-018)

    // O coração da admissão atômica: uma rajada simultânea contra a mesma conta NÃO pode passar pela consulta antes de
    // qualquer tentativa ser contada. Com o relógio parado, de 50 chamadas só 5 seguem.
    [Fact]
    public async Task Cinquenta_chamadas_concorrentes_a_mesma_conta_admitem_exatamente_cinco()
    {
        var throttle = Create();
        var gate = new TaskCompletionSource();

        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(async () =>
            {
                await gate.Task;                  // todas largam juntas
                return await TryAsync(throttle);
            }))
            .ToArray();

        await Task.Yield();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.Equal(5, results.Count(r => r is null));
        Assert.Equal(45, results.Count(r => r is { } espera && espera > TimeSpan.Zero));
    }

    [Fact]
    public async Task Contas_diferentes_em_paralelo_nao_se_atrapalham()
    {
        var throttle = Create();
        var gate = new TaskCompletionSource();

        // 100 contas x 5 tentativas simultâneas: cada conta tem direito às suas 5.
        var tasks = Enumerable.Range(0, 100)
            .SelectMany(conta => Enumerable.Range(0, 5).Select(_ => Task.Run(async () =>
            {
                await gate.Task;
                return await TryAsync(throttle, $"conta{conta}@test.com");
            })))
            .ToArray();

        await Task.Yield();
        gate.SetResult();
        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.Null(r));
    }

    #endregion

    #region Saturação (FR-022: falha fechada)

    private static LoginThrottleOptions Small() => new() { MaxTrackedAccounts = 100 };

    private async Task FillAsync(InMemoryLoginThrottle throttle, int attemptsPerAccount, TimeSpan? advanceBetween = null)
    {
        for (var i = 0; i < 100; i++)
        {
            for (var n = 0; n < attemptsPerAccount; n++) await TryAsync(throttle, $"a{i}@test.com");
            if (advanceBetween is { } step) _clock.Advance(step);
        }
    }

    [Fact]
    public async Task Registro_cheio_de_contas_nao_bloqueadas_abre_espaco_removendo_as_mais_antigas()
    {
        var throttle = Create(Small());
        await FillAsync(throttle, attemptsPerAccount: 4, advanceBetween: TimeSpan.FromSeconds(1));
        Assert.Equal(100, Entries(throttle).Count);

        Assert.Null(await TryAsync(throttle, "novo@test.com"));   // a 101ª é admitida: abriu espaço

        // A mais antiga (a0, com 4 tentativas) foi removida: recomeça do zero e as 2 próximas passam.
        Assert.Null(await TryAsync(throttle, "a0@test.com"));
        Assert.Null(await TryAsync(throttle, "a0@test.com"));
        // A mais recente (a99, com 4 tentativas) foi mantida: a 5ª passa e a 6ª já é bloqueada.
        Assert.Null(await TryAsync(throttle, "a99@test.com"));
        Assert.NotNull(await TryAsync(throttle, "a99@test.com"));
    }

    [Fact]
    public async Task Registro_cheio_de_contas_bloqueadas_nao_remove_nenhuma_e_bloqueia_conta_nova_por_60_segundos()
    {
        var throttle = Create(Small());
        await FillAsync(throttle, attemptsPerAccount: 5);   // 100 contas, todas bloqueadas
        Assert.Equal(100, Entries(throttle).Count);

        // A 101ª não cabe e NÃO pode expulsar uma bloqueada: recebe 60 s, sem ser contada.
        Assert.Equal(TimeSpan.FromSeconds(60), await TryAsync(throttle, "nova@test.com"));
        Assert.Equal(TimeSpan.FromSeconds(60), await TryAsync(throttle, "nova@test.com"));

        Assert.Equal(100, Entries(throttle).Count);                       // nada entrou, nada saiu
        for (var i = 0; i < 100; i++)
            Assert.NotNull(await TryAsync(throttle, $"a{i}@test.com"));   // as 100 continuam bloqueadas
    }

    [Fact]
    public async Task Registro_saturado_nunca_devolve_null_para_conta_nova()
    {
        var throttle = Create(Small());
        await FillAsync(throttle, attemptsPerAccount: 5);

        for (var i = 0; i < 20; i++)
            Assert.NotNull(await TryAsync(throttle, $"desconhecida{i}@test.com"));
    }

    [Fact]
    public async Task Saturacao_acaba_quando_registros_expiram()
    {
        var throttle = Create(Small());
        await FillAsync(throttle, attemptsPerAccount: 5);
        Assert.NotNull(await TryAsync(throttle, "nova@test.com"));

        // 60 s de bloqueio + 15 min sem nenhuma tentativa: todos os registros expiram.
        _clock.Advance(TimeSpan.FromSeconds(60) + TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        Assert.Null(await TryAsync(throttle, "nova@test.com"));
    }

    #endregion
}
