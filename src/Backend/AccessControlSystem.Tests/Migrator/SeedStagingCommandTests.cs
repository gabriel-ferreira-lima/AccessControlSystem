using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Migrator;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Tests.Migrator;

// seed-staging: dados FICTÍCIOS de homologação (data-model.md, seção 4). Só roda em Staging, é idempotente, registra
// as bases e os tenants no catálogo, atualiza a estrutura das bases (sem o administrador fixo) e grava os operadores
// pelo AppDbContext, sob RLS, como acs_app. Nunca usa o e-mail do administrador fixo e nunca imprime senha.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class SeedStagingCommandTests
{
    private const string Verb = "seed-staging";

    // Literais escritos aqui, independentes do código de produção (pegam mudança acidental dos ids estáveis).
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000001");
    private static readonly Guid TenantB = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000002");
    private static readonly Guid TenantD = Guid.Parse("aaaaaaaa-0000-4000-8000-000000000003");
    private const string FixedAdminId = "cc59e349-efbd-4800-9726-f38a325e4d3d";
    private const string FixedAdminEmail = "admin@admin.com.br";

    private readonly PostgresContainerFixture _fixture;

    public SeedStagingCommandTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private Task<StagingEnvironment> NewEnvironmentAsync(string? environmentName = "Staging") =>
        StagingTestSupport.NewEnvironmentAsync(_fixture, environmentName);

    private async Task AssertNadaFoiEscritoAsync(StagingEnvironment environment)
    {
        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        Assert.Empty(await catalog.Tenants.ToListAsync());
        Assert.Empty(await catalog.Databases.ToListAsync());
        Assert.Equal(0L, await StagingTestSupport.CountTablesAsync(environment.Shared.Migrator));
        Assert.Equal(0L, await StagingTestSupport.CountTablesAsync(environment.Dedicated.Migrator));
    }

    [Fact]
    public async Task Parte_de_bases_vazias_registra_atualiza_e_cria_os_tres_tenants_e_os_operadores()
    {
        var environment = await NewEnvironmentAsync();
        await AssertNadaFoiEscritoAsync(environment);   // ponto de partida: sem registro no catálogo e sem estrutura

        var code = await StagingTestSupport.RunAsync(Verb, environment);

        Assert.Equal(ExitCodes.Ok, code);

        // Catálogo: as duas bases registradas e já atualizadas, e os três tenants ativos com os ids estáveis.
        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        var databases = await catalog.Databases.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        Assert.Equal(new[] { "dedicated_01", "shared_01" }, databases.Select(x => x.Name));
        Assert.Equal(EDatabaseKind.Dedicated, databases[0].Kind);
        Assert.Equal(EDatabaseKind.Shared, databases[1].Kind);
        Assert.All(databases, d => Assert.Equal(EMigrationStatus.Succeeded, d.MigrationStatus));

        var tenants = await catalog.Tenants.AsNoTracking().Include(x => x.Database).ToDictionaryAsync(x => x.Id);
        Assert.Equal(3, tenants.Count);
        Assert.Equal(("99999999000101", "Tenant A (homologação)", "shared_01"),
            (tenants[TenantA].Cnpj, tenants[TenantA].Name, tenants[TenantA].Database.Name));
        Assert.Equal(("99999999000102", "Tenant B (homologação)", "shared_01"),
            (tenants[TenantB].Cnpj, tenants[TenantB].Name, tenants[TenantB].Database.Name));
        Assert.Equal(("99999999000103", "Tenant D (homologação)", "dedicated_01"),
            (tenants[TenantD].Cnpj, tenants[TenantD].Name, tenants[TenantD].Database.Name));
        Assert.All(tenants.Values, t => Assert.True(t.IsActive));

        // Operadores (lidos pela conexão do migrator, que enxerga todos os tenants).
        var shared = await StagingTestSupport.ReadOperatorsAsync(environment.Shared.Migrator);
        var dedicated = await StagingTestSupport.ReadOperatorsAsync(environment.Dedicated.Migrator);

        Assert.Equal(4, shared.Count);
        Assert.Single(dedicated);
        Assert.Contains(shared, o => o.TenantId == TenantA && o.Email == "admin@homologacao.test");
        Assert.Contains(shared, o => o.TenantId == TenantA && o.Email == "operador@homologacao.test");
        Assert.Contains(shared, o => o.TenantId == TenantB && o.Email == "admin@homologacao.test");
        Assert.Contains(shared, o => o.TenantId == TenantB && o.Email == "operador@homologacao.test");
        Assert.Contains(dedicated, o => o.TenantId == TenantD && o.Email == "admin@homologacao.test");

        // Mesmo e-mail em tenants diferentes, com senhas diferentes (hashes diferentes).
        var operatorA = shared.Single(o => o.TenantId == TenantA && o.Email == "operador@homologacao.test");
        var operatorB = shared.Single(o => o.TenantId == TenantB && o.Email == "operador@homologacao.test");
        Assert.NotEqual(operatorA.Password, operatorB.Password);
    }

    [Fact]
    public async Task E_idempotente_e_nao_troca_a_senha_de_ninguem()
    {
        var environment = await NewEnvironmentAsync();
        Assert.Equal(ExitCodes.Ok, await StagingTestSupport.RunAsync(Verb, environment));
        var sharedBefore = await StagingTestSupport.ReadOperatorsAsync(environment.Shared.Migrator);
        var dedicatedBefore = await StagingTestSupport.ReadOperatorsAsync(environment.Dedicated.Migrator);

        // A segunda execução usa OUTRA senha válida: se o comando trocasse senha, o hash mudaria.
        environment.Settings["StagingSeed:AdminPassword"] = "Outra!Senha-777";
        var second = await StagingTestSupport.RunAsync(Verb, environment);

        Assert.Equal(ExitCodes.Ok, second);
        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        Assert.Equal(3, await catalog.Tenants.CountAsync());
        Assert.Equal(2, await catalog.Databases.CountAsync());
        Assert.Equal(sharedBefore, await StagingTestSupport.ReadOperatorsAsync(environment.Shared.Migrator));
        Assert.Equal(dedicatedBefore, await StagingTestSupport.ReadOperatorsAsync(environment.Dedicated.Migrator));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    [InlineData(null)]       // sem ambiente definido vale Production
    [InlineData("Qa")]
    public async Task Recusa_com_5_fora_de_Staging_sem_escrever_nada(string? environmentName)
    {
        var environment = await NewEnvironmentAsync(environmentName);
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment, output);

        Assert.Equal(ExitCodes.SeedStagingRefused, code);
        Assert.Contains(environmentName ?? "Production", output.ToString());   // a mensagem cita o ambiente
        await AssertNadaFoiEscritoAsync(environment);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task Recusa_antes_de_ler_a_configuracao_mesmo_com_ela_invalida(string environmentName)
    {
        var environment = await NewEnvironmentAsync(environmentName);
        environment.Settings["ConnectionStrings:CatalogMigration"] = null;   // configuração inválida...
        environment.Settings["StagingSeed:AdminPassword"] = null;

        var code = await StagingTestSupport.RunAsync(Verb, environment);

        Assert.Equal(ExitCodes.SeedStagingRefused, code);   // ...e ainda assim é 5, não 2
    }

    [Fact]
    public async Task Aceita_staging_escrito_em_minusculas()
    {
        var environment = await NewEnvironmentAsync("staging");

        Assert.Equal(ExitCodes.Ok, await StagingTestSupport.RunAsync(Verb, environment));
    }

    [Theory]
    [InlineData("StagingSeed:AdminPassword", null)]
    [InlineData("StagingSeed:AdminPassword", "Fraca1!")]            // 7 caracteres: não atende às regras de senha
    [InlineData("StagingSeed:OperatorPasswordTenantA", null)]
    [InlineData("StagingSeed:OperatorPasswordTenantB", "sem-numero-nem-maiuscula")]
    public async Task Sai_com_2_quando_a_senha_falta_ou_e_fraca_sem_criar_nada_e_sem_imprimir_o_valor(string key, string? value)
    {
        var environment = await NewEnvironmentAsync();
        environment.Settings[key] = value;
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment, output);

        Assert.Equal(ExitCodes.InvalidConfiguration, code);
        await AssertNadaFoiEscritoAsync(environment);
        if (value is not null) Assert.DoesNotContain(value, output.ToString());
    }

    [Fact]
    public async Task Nenhum_operador_usa_o_email_do_administrador_fixo_e_todos_usam_dominio_de_teste()
    {
        var environment = await NewEnvironmentAsync();
        await StagingTestSupport.RunAsync(Verb, environment);

        var all = (await StagingTestSupport.ReadOperatorsAsync(environment.Shared.Migrator))
            .Concat(await StagingTestSupport.ReadOperatorsAsync(environment.Dedicated.Migrator))
            .ToList();

        Assert.Equal(5, all.Count);
        Assert.DoesNotContain(all, o => string.Equals(o.Email, FixedAdminEmail, StringComparison.OrdinalIgnoreCase));
        Assert.All(all, o => Assert.EndsWith(".test", o.Email));
    }

    [Fact]
    public async Task As_bases_nascem_sem_o_administrador_fixo_da_migration()
    {
        var environment = await NewEnvironmentAsync();
        await StagingTestSupport.RunAsync(Verb, environment);

        foreach (var database in new[] { environment.Shared, environment.Dedicated })
        {
            Assert.Equal(0L, await DatabaseProbe.CountAsync(database.Migrator,
                $"select count(*) from operators where id = '{FixedAdminId}'"));
            // A SeedAdminPolicy marcou a migration como aplicada SEM executá-la.
            Assert.Contains("20260908171522_SeedAdmin", await DatabaseProbe.ReadHistoryAsync(database.Migrator));
        }
    }

    [Fact]
    public async Task Falha_na_atualizacao_de_uma_base_sai_com_1_sem_operador_algum_e_a_nova_execucao_conclui()
    {
        var environment = await NewEnvironmentAsync();
        const string wrongPassword = "senha-errada";
        environment.Settings["TenantDatabases:dedicated_01:MigrationConnectionString"] =
            environment.Dedicated.Migrator.Replace(PostgresRoleBootstrap.MigratorPassword, wrongPassword);
        var output = new StringWriter();

        var failed = await StagingTestSupport.RunAsync(Verb, environment, output);

        Assert.Equal(ExitCodes.SomeDatabaseFailed, failed);
        // Nenhum operador em NENHUMA base (nem na compartilhada, que foi atualizada com sucesso).
        Assert.Empty(await StagingTestSupport.ReadOperatorsAsync(environment.Shared.Migrator));
        Assert.Empty(await StagingTestSupport.ReadOperatorsAsync(environment.Dedicated.Migrator));
        // A saída diz QUAL base falhou, sem connection string nem senha.
        Assert.Contains("dedicated_01", output.ToString());
        Assert.DoesNotContain(wrongPassword, output.ToString());
        Assert.DoesNotContain(PostgresRoleBootstrap.MigratorPassword, output.ToString());

        // O registro no catálogo PODE ter ficado (é idempotente; o teste não afirma o contrário). Com a causa
        // corrigida, uma nova execução conclui o trabalho sem duplicar nada (FR-030).
        environment.Settings["TenantDatabases:dedicated_01:MigrationConnectionString"] = environment.Dedicated.Migrator;
        var second = await StagingTestSupport.RunAsync(Verb, environment);

        Assert.Equal(ExitCodes.Ok, second);
        await using var catalog = _fixture.CreateCatalogContext(environment.Catalog.Migrator);
        Assert.Equal(3, await catalog.Tenants.CountAsync());
        Assert.Equal(2, await catalog.Databases.CountAsync());
        Assert.Equal(4, (await StagingTestSupport.ReadOperatorsAsync(environment.Shared.Migrator)).Count);
        Assert.Single(await StagingTestSupport.ReadOperatorsAsync(environment.Dedicated.Migrator));
    }

    [Fact]
    public async Task A_saida_nao_contem_nenhuma_das_tres_senhas()
    {
        var environment = await NewEnvironmentAsync();
        var output = new StringWriter();

        Assert.Equal(ExitCodes.Ok, await StagingTestSupport.RunAsync(Verb, environment, output));

        var text = output.ToString();
        Assert.False(string.IsNullOrWhiteSpace(text));
        Assert.DoesNotContain(StagingTestSupport.AdminPassword, text);
        Assert.DoesNotContain(StagingTestSupport.OperatorPasswordA, text);
        Assert.DoesNotContain(StagingTestSupport.OperatorPasswordB, text);
    }
}
