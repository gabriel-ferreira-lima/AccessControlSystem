using System.Collections.Concurrent;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Infrastructure.Data;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using AccessControlSystem.Migrator.Options;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AccessControlSystem.Tests.Migrator;

// O orquestrador usa o catálogo REAL (um catálogo isolado por teste) e um IDatabaseMigrationRunner falso:
// o que se prova aqui é a orquestração (lotes, concorrência, retomada, falha isolada, trava global).
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class MigrationOrchestratorTests
{
    private static readonly string Latest = AppMigrations.LatestId();

    private readonly PostgresContainerFixture _fixture;

    public MigrationOrchestratorTests(PostgresContainerFixture fixture) => _fixture = fixture;

    #region Montagem

    /// <summary>Registro que anota o tamanho de cada página pedida ao catálogo.</summary>
    private sealed class RecordingRegistry : CatalogRegistry
    {
        public ConcurrentQueue<int> PageSizes { get; } = new();

        public RecordingRegistry(DbContextOptions<CatalogDbContext> options) : base(options)
        {
        }

        public override async Task<IReadOnlyList<CatalogDatabase>> ListEligibleAsync(
            Guid? afterId, int size, string latestMigrationId, CancellationToken cancellationToken = default)
        {
            var page = await base.ListEligibleAsync(afterId, size, latestMigrationId, cancellationToken);
            PageSizes.Enqueue(page.Count);
            return page;
        }
    }

    private sealed class Harness
    {
        public required DatabaseConnectionStrings Catalog { get; init; }
        public required RecordingRegistry Registry { get; init; }
        public required IDatabaseMigrationRunner Runner { get; init; }
        public TenantDatabasesOptions Databases { get; } = new();

        public MigrationOrchestrator Create(
            int batchSize = 10, int parallelism = 2, int lockTimeoutSeconds = 30, InitialTenantOptions? initial = null) =>
            new(Registry, Runner,
                Options.Create(Databases),
                Options.Create(new MigratorOptions
                {
                    BatchSize = batchSize, MaxDegreeOfParallelism = parallelism, LockTimeoutSeconds = lockTimeoutSeconds
                }),
                Options.Create(new CatalogMigrationOptions { CatalogMigration = Catalog.Migrator }),
                Options.Create(initial ?? new InitialTenantOptions()));
    }

    private async Task<Harness> NewHarnessAsync()
    {
        var catalog = await _fixture.CreateIsolatedCatalogAsync();
        return new Harness
        {
            Catalog = catalog,
            Registry = new RecordingRegistry(PostgresContainerFixture.CatalogOptions(catalog.Migrator)),
            Runner = Substitute.For<IDatabaseMigrationRunner>()
        };
    }

    // A connection string de migração das bases falsas carrega só o nome da base, que o executor falso
    // usa para decidir o que fazer.
    private static string FakeConnectionString(string name) =>
        $"Host=falso;Database={name};Username=acs_migrator;Password=segredo-de-{name}";

    private async Task<List<CatalogDatabase>> AddAsync(Harness harness, int count, string prefix = "base")
    {
        var databases = await MigratorTestSupport.AddDatabasesAsync(_fixture, harness.Catalog.Migrator, count, prefix);
        foreach (var database in databases)
        {
            harness.Databases[database.Name] = new TenantDatabaseOptions
            {
                MigrationConnectionString = FakeConnectionString(database.Name)
            };
        }
        return databases;
    }

    private static string NameOf(string connectionString) => new NpgsqlConnectionStringBuilder(connectionString).Database!;

    private static MigrationRunResult Ok() => new(true, Latest, null);

    /// <summary>Executor falso: sucesso, salvo para as bases em <paramref name="failing"/>. Anota as chamadas.</summary>
    private static ConcurrentQueue<(string Name, Guid? InitialTenant)> FakeRunner(
        Harness harness, ISet<string>? failing = null, TimeSpan? delay = null, Action<string>? onStart = null, Action<string>? onEnd = null)
    {
        var calls = new ConcurrentQueue<(string, Guid?)>();
        harness.Runner
            .RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>())
            .Returns(async call =>
            {
                var name = NameOf(call.ArgAt<string>(0));
                calls.Enqueue((name, call.ArgAt<Guid?>(2)));
                onStart?.Invoke(name);
                try
                {
                    if (delay is not null) await Task.Delay(delay.Value);
                    return failing is not null && failing.Contains(name)
                        ? new MigrationRunResult(false, null, "falha simulada")
                        : Ok();
                }
                finally
                {
                    onEnd?.Invoke(name);
                }
            });
        return calls;
    }

    private Task<CatalogDatabase> ReadAsync(Harness harness, Guid id) =>
        MigratorTestSupport.ReadDatabaseAsync(_fixture, harness.Catalog.Migrator, id);

    #endregion

    [Fact]
    public async Task Processa_em_lotes_do_tamanho_configurado()
    {
        var harness = await NewHarnessAsync();
        await AddAsync(harness, 25);
        FakeRunner(harness);

        await harness.Create(batchSize: 10).RunAsync();

        Assert.Equal(new[] { 10, 10, 5 }, harness.Registry.PageSizes.Where(x => x > 0));
    }

    [Fact]
    public async Task Respeita_a_concorrencia_maxima()
    {
        var harness = await NewHarnessAsync();
        await AddAsync(harness, 12);
        var running = 0;
        var maximum = 0;
        FakeRunner(harness, delay: TimeSpan.FromMilliseconds(150),
            onStart: _ => { var now = Interlocked.Increment(ref running); InterlockedMax(ref maximum, now); },
            onEnd: _ => Interlocked.Decrement(ref running));

        await harness.Create(batchSize: 12, parallelism: 3).RunAsync();

        Assert.InRange(maximum, 2, 3);   // houve paralelismo, e nunca passou do limite
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)))
            Interlocked.CompareExchange(ref target, value, current);
    }

    [Fact]
    public async Task Atualiza_todas_as_bases_e_registra_a_versao()
    {
        var harness = await NewHarnessAsync();
        var databases = await AddAsync(harness, 7);
        FakeRunner(harness);

        var summary = await harness.Create().RunAsync();

        Assert.Equal(7, summary.Updated);
        Assert.Equal(0, summary.Failed);
        foreach (var database in databases)
        {
            var stored = await ReadAsync(harness, database.Id);
            Assert.Equal(EMigrationStatus.Succeeded, stored.MigrationStatus);
            Assert.Equal(Latest, stored.SchemaVersion);
            Assert.NotNull(stored.MigrationAttemptedAt);
        }
    }

    [Fact]
    public async Task Falha_de_uma_base_nao_impede_as_outras()
    {
        var harness = await NewHarnessAsync();
        var databases = await AddAsync(harness, 6);
        var broken = databases[2];
        FakeRunner(harness, failing: new HashSet<string> { broken.Name });

        var summary = await harness.Create(batchSize: 6).RunAsync();

        Assert.Equal(5, summary.Updated);
        Assert.Equal(1, summary.Failed);
        var outcome = Assert.Single(summary.Outcomes, x => x.State == EMigrationStatus.Failed);
        Assert.Equal(broken.Name, outcome.Name);
        Assert.Equal("falha simulada", outcome.Reason);

        var stored = await ReadAsync(harness, broken.Id);
        Assert.Equal(EMigrationStatus.Failed, stored.MigrationStatus);
        Assert.Equal("falha simulada", stored.MigrationError);
        foreach (var other in databases.Where(x => x.Id != broken.Id))
            Assert.Equal(EMigrationStatus.Succeeded, (await ReadAsync(harness, other.Id)).MigrationStatus);
    }

    [Fact]
    public async Task Segunda_execucao_so_reprocessa_pendentes_e_falhas()
    {
        var harness = await NewHarnessAsync();
        var databases = await AddAsync(harness, 5);
        var broken = databases[1].Name;
        FakeRunner(harness, failing: new HashSet<string> { broken });
        await harness.Create().RunAsync();

        // Segunda execução: tudo funciona; só a que falhou pode ser reprocessada.
        harness.Runner.ClearReceivedCalls();
        var calls = FakeRunner(harness);
        var summary = await harness.Create().RunAsync();

        Assert.Equal(new[] { broken }, calls.Select(x => x.Name));
        Assert.Equal(1, summary.Updated);
        Assert.Equal(4, summary.AlreadyCurrent);
    }

    [Fact]
    public async Task Base_Running_de_queda_e_reprocessada()
    {
        var harness = await NewHarnessAsync();
        var database = (await AddAsync(harness, 1))[0];
        await harness.Registry.MarkRunningAsync(database.Id);   // sobra de uma queda do processo
        var calls = FakeRunner(harness);

        await harness.Create().RunAsync();

        Assert.Equal(new[] { database.Name }, calls.Select(x => x.Name));
        Assert.Equal(EMigrationStatus.Succeeded, (await ReadAsync(harness, database.Id)).MigrationStatus);
    }

    [Fact]
    public async Task Base_nova_no_catalogo_e_atualizada_sem_mudar_o_passo()
    {
        var harness = await NewHarnessAsync();
        await AddAsync(harness, 3, "antiga");
        var calls = FakeRunner(harness);
        await harness.Create().RunAsync();

        var novas = await AddAsync(harness, 1, "nova");   // cadastrada depois, por outro sistema
        var summary = await harness.Create().RunAsync();

        Assert.Equal(1, summary.Updated);
        Assert.Equal(3, summary.AlreadyCurrent);
        Assert.Equal(novas[0].Name, calls.Last().Name);
        Assert.Equal(4, calls.Count);   // 3 na primeira execução + 1 na segunda
    }

    [Fact]
    public async Task Duas_execucoes_simultaneas_nao_atualizam_a_mesma_base_ao_mesmo_tempo()
    {
        var harness = await NewHarnessAsync();
        await AddAsync(harness, 6);
        var runningByName = new ConcurrentDictionary<string, int>();
        var overlapped = false;
        var calls = FakeRunner(harness, delay: TimeSpan.FromMilliseconds(80),
            onStart: name => { if (runningByName.AddOrUpdate(name, 1, (_, v) => v + 1) > 1) overlapped = true; },
            onEnd: name => runningByName.AddOrUpdate(name, 0, (_, v) => v - 1));

        // Dois orquestradores no mesmo catálogo: a trava global serializa as execuções.
        var first = harness.Create(lockTimeoutSeconds: 60).RunAsync();
        var second = harness.Create(lockTimeoutSeconds: 60).RunAsync();
        await Task.WhenAll(first, second);

        Assert.False(overlapped);
        Assert.Equal(6, calls.Count);   // cada base foi atualizada uma única vez; a 2ª execução não encontrou o que fazer
    }

    [Fact]
    public async Task Trava_global_esgotada_lanca_e_nao_atualiza_nada()
    {
        var harness = await NewHarnessAsync();
        await AddAsync(harness, 2);
        var calls = FakeRunner(harness);

        // Outra execução (aqui, uma conexão qualquer) já segura a trava global do catálogo.
        await using var holder = new NpgsqlConnection(harness.Catalog.Migrator);
        await holder.OpenAsync();
        await using (var lockCommand = new NpgsqlCommand("SELECT pg_advisory_lock(@key)", holder))
        {
            lockCommand.Parameters.AddWithValue("key", MigrationOrchestrator.GlobalLockKey);
            await lockCommand.ExecuteNonQueryAsync();
        }

        await Assert.ThrowsAsync<MigratorLockTimeoutException>(() => harness.Create(lockTimeoutSeconds: 1).RunAsync());

        Assert.Empty(calls);
    }

    [Fact]
    public async Task Motivo_da_falha_nao_contem_connection_string()
    {
        var harness = await NewHarnessAsync();
        var databases = await AddAsync(harness, 2);
        var vazando = databases[0].Name;
        var lancando = databases[1].Name;
        harness.Runner
            .RunAsync(Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<Guid?>())
            .Returns(call =>
            {
                var connectionString = call.ArgAt<string>(0);
                var name = NameOf(connectionString);
                // Um executor mal-comportado devolve (ou lança) o texto com a connection string inteira.
                if (name == vazando) return Task.FromResult(new MigrationRunResult(false, null, $"erro ao conectar com {connectionString}"));
                throw new InvalidOperationException($"falha em {connectionString}");
            });

        var summary = await harness.Create().RunAsync();

        Assert.Equal(2, summary.Failed);
        foreach (var database in databases)
        {
            var reason = (await ReadAsync(harness, database.Id)).MigrationError!;
            Assert.DoesNotContain("Host=falso", reason);
            Assert.DoesNotContain($"segredo-de-{database.Name}", reason);
        }
        foreach (var outcome in summary.Outcomes)
        {
            Assert.DoesNotContain("segredo-de-", outcome.Reason!);
            Assert.DoesNotContain("Host=falso", outcome.Reason!);
        }
    }

    [Fact]
    public async Task Base_sem_connection_string_de_migracao_vira_Failed()
    {
        var harness = await NewHarnessAsync();
        var databases = await AddAsync(harness, 3);
        harness.Databases.Remove(databases[1].Name);   // sem MigrationConnectionString configurada
        var calls = FakeRunner(harness);

        var summary = await harness.Create().RunAsync();

        var stored = await ReadAsync(harness, databases[1].Id);
        Assert.Equal(EMigrationStatus.Failed, stored.MigrationStatus);
        Assert.Contains(databases[1].Name, stored.MigrationError!);
        Assert.Contains("MigrationConnectionString", stored.MigrationError!);
        Assert.DoesNotContain(calls, x => x.Name == databases[1].Name);   // nunca chegou ao executor
        Assert.Equal(2, summary.Updated);   // as outras duas foram atualizadas
        Assert.Equal(1, summary.Failed);
    }

    [Fact]
    public async Task Base_do_tenant_inicial_recebe_o_id_do_tenant_inicial_e_as_outras_nao()
    {
        var harness = await NewHarnessAsync();
        var databases = await AddAsync(harness, 3);
        var calls = FakeRunner(harness);
        var initial = new InitialTenantOptions
        {
            Id = Guid.NewGuid(), Cnpj = "x", Name = "x", DatabaseName = databases[1].Name
        };

        await harness.Create(initial: initial).RunAsync();

        Assert.Equal(initial.Id, calls.Single(x => x.Name == databases[1].Name).InitialTenant);
        Assert.All(calls.Where(x => x.Name != databases[1].Name), x => Assert.Null(x.InitialTenant));
    }

    [Fact]
    public async Task Mil_bases_com_uma_falha_atualizam_as_outras_999_e_a_reexecucao_processa_so_a_que_falhou()
    {
        // SC-006 com executor falso (a medição de tempo com bases reais é da spec 06).
        var harness = await NewHarnessAsync();
        var databases = await AddAsync(harness, 1000, "mil");
        var broken = databases[500].Name;
        var firstCalls = FakeRunner(harness, failing: new HashSet<string> { broken });

        var first = await harness.Create(batchSize: 100, parallelism: 8).RunAsync();

        Assert.Equal(999, first.Updated);
        Assert.Equal(1, first.Failed);
        Assert.Equal(1000, firstCalls.Count);

        harness.Runner.ClearReceivedCalls();
        var secondCalls = FakeRunner(harness);
        var second = await harness.Create(batchSize: 100, parallelism: 8).RunAsync();

        Assert.Equal(new[] { broken }, secondCalls.Select(x => x.Name));
        Assert.Equal(1, second.Updated);
        Assert.Equal(999, second.AlreadyCurrent);
    }
}
