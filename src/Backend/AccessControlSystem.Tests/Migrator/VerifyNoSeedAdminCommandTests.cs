using AccessControlSystem.Migrator;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Npgsql;

namespace AccessControlSystem.Tests.Migrator;

// verify-no-seed-admin: barreira independente da SeedAdminPolicy. Percorre o catálogo e TODAS as bases e reprova (7)
// se QUALQUER dado fixo da migration SeedAdmin aparecer (id fixo OU e-mail fixo, sem diferenciar maiúsculas), reprova
// (8) o que não consegue verificar e recusa Development (6). Só lê. A saída nunca traz segredo, e-mail nem id.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class VerifyNoSeedAdminCommandTests
{
    private const string Verb = "verify-no-seed-admin";

    // Literais escritos aqui, independentes do código de produção.
    private const string FixedId = "cc59e349-efbd-4800-9726-f38a325e4d3d";
    private const string FixedEmail = "admin@admin.com.br";
    private const string ClosedPort = "Host=127.0.0.1;Port=1;Database=nao_existe;Username=acs_migrator;Password=x;Timeout=2;Command Timeout=2";

    private readonly PostgresContainerFixture _fixture;

    public VerifyNoSeedAdminCommandTests(PostgresContainerFixture fixture) => _fixture = fixture;

    /// <summary>Catálogo isolado (migrado) e a configuração mínima do verbo; as bases entram com <see cref="AddAsync"/>.</summary>
    private async Task<(DatabaseConnectionStrings Catalog, Dictionary<string, string?> Settings)> NewEnvironmentAsync(
        string environmentName = "Staging")
    {
        var catalog = await _fixture.CreateIsolatedCatalogAsync();
        var settings = new Dictionary<string, string?>
        {
            ["DOTNET_ENVIRONMENT"] = environmentName,
            ["ConnectionStrings:CatalogMigration"] = catalog.Migrator,
        };
        return (catalog, settings);
    }

    /// <summary>Cadastra a base no catálogo E aponta a configuração para ela (o caso normal de um ambiente).</summary>
    private async Task AddAsync(
        (DatabaseConnectionStrings Catalog, Dictionary<string, string?> Settings) environment,
        string name, DatabaseConnectionStrings database)
    {
        await StagingTestSupport.RegisterDatabaseAsync(_fixture, environment.Catalog.Migrator, name);
        StagingTestSupport.ConfigureDatabase(environment.Settings, name, database);
    }

    private static string NewName(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..28];

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static void AssertSaidaSemDadoSensivel(string output)
    {
        Assert.DoesNotContain(FixedEmail, output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cc59e349", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(PostgresRoleBootstrap.MigratorPassword, output);
        Assert.DoesNotContain("Host=", output);
    }

    #region 1. Aprovação

    [Fact]
    public async Task Aprova_com_0_quando_todas_as_bases_estao_limpas_e_mostra_a_contagem()
    {
        var environment = await NewEnvironmentAsync();
        await AddAsync(environment, NewName("limpa_a"), await _fixture.CreateMigratedTenantDatabaseAsync(NewName("vm_a")));
        await AddAsync(environment, NewName("limpa_b"), await _fixture.CreateMigratedTenantDatabaseAsync(NewName("vm_b")));
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings, output);

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Contains("Bases de tenant verificadas: 2", output.ToString());
        AssertSaidaSemDadoSensivel(output.ToString());
    }

    [Fact]
    public async Task Aprova_um_catalogo_sem_nenhuma_base_cadastrada()
    {
        var environment = await NewEnvironmentAsync();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings);

        Assert.Equal(ExitCodes.Ok, code);
    }

    #endregion

    #region 2 a 4. Base de tenant contaminada (qualquer dado fixo basta)

    [Fact]
    public async Task Reprova_com_7_a_base_de_tenant_contaminada_de_verdade_citando_so_o_nome_logico()
    {
        var environment = await NewEnvironmentAsync();
        var dirty = NewName("suja");
        await AddAsync(environment, dirty, await StagingTestSupport.ContaminatedDatabaseAsync(_fixture, NewName("vc_a")));
        await AddAsync(environment, NewName("limpa"), await _fixture.CreateMigratedTenantDatabaseAsync(NewName("vm_c")));
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings, output);

        Assert.Equal(ExitCodes.FixedAdminFound, code);
        Assert.Contains(dirty, output.ToString());
        AssertSaidaSemDadoSensivel(output.ToString());
    }

    [Fact]
    public async Task Reprova_com_7_quando_o_email_foi_alterado_e_o_id_fixo_continua()
    {
        var environment = await NewEnvironmentAsync();
        var database = await StagingTestSupport.ContaminatedDatabaseAsync(_fixture, NewName("vc_b"));
        await ExecuteAsync(database.Migrator, $"update operators set email = 'outro@x.test' where id = '{FixedId}'");
        await AddAsync(environment, NewName("alterada"), database);

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings);

        Assert.Equal(ExitCodes.FixedAdminFound, code);
    }

    [Fact]
    public async Task Reprova_com_7_quando_o_id_foi_trocado_e_o_email_fixo_continua_com_outras_maiusculas()
    {
        var environment = await NewEnvironmentAsync();
        var database = await StagingTestSupport.ContaminatedDatabaseAsync(_fixture, NewName("vc_c"));
        await ExecuteAsync(database.Migrator,
            $"update operators set id = '{Guid.NewGuid()}', email = 'ADMIN@Admin.com.BR' where id = '{FixedId}'");
        await AddAsync(environment, NewName("trocada"), database);

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings);

        Assert.Equal(ExitCodes.FixedAdminFound, code);
    }

    #endregion

    #region 5. Catálogo contaminado

    [Theory]
    [InlineData("create table prova_email (email text); insert into prova_email values ('Admin@Admin.com.br')")]
    [InlineData("create table prova_operador (operator_id uuid); insert into prova_operador values ('cc59e349-efbd-4800-9726-f38a325e4d3d')")]
    public async Task Reprova_com_7_quando_o_catalogo_contem_o_email_ou_o_id_fixo(string sql)
    {
        var environment = await NewEnvironmentAsync();
        await ExecuteAsync(environment.Catalog.Migrator, sql);
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings, output);

        Assert.Equal(ExitCodes.FixedAdminFound, code);
        Assert.Contains("catalog", output.ToString());
        AssertSaidaSemDadoSensivel(output.ToString());
    }

    #endregion

    #region 6. "Não contém"

    [Fact]
    public async Task Base_sem_as_tabelas_de_operadores_conta_como_nao_contem()
    {
        var environment = await NewEnvironmentAsync();
        await AddAsync(environment, NewName("vazia"), await _fixture.CreateEmptyDatabaseAsync(NewName("ve_a")));

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings);

        Assert.Equal(ExitCodes.Ok, code);
    }

    #endregion

    #region 7. Não foi possível verificar

    [Fact]
    public async Task Base_inacessivel_sai_com_8_citando_o_nome_sem_connection_string()
    {
        var environment = await NewEnvironmentAsync();
        var name = NewName("fora");
        await StagingTestSupport.RegisterDatabaseAsync(_fixture, environment.Catalog.Migrator, name);
        environment.Settings[$"TenantDatabases:{name}:MigrationConnectionString"] = ClosedPort;
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings, output);

        Assert.Equal(ExitCodes.CannotVerify, code);
        Assert.Contains(name, output.ToString());
        AssertSaidaSemDadoSensivel(output.ToString());
    }

    [Fact]
    public async Task Base_cadastrada_no_catalogo_sem_connection_string_sai_com_8()
    {
        var environment = await NewEnvironmentAsync();
        var name = NewName("sem_cs");
        await StagingTestSupport.RegisterDatabaseAsync(_fixture, environment.Catalog.Migrator, name);   // sem configuração
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings, output);

        Assert.Equal(ExitCodes.CannotVerify, code);
        Assert.Contains(name, output.ToString());
    }

    [Fact]
    public async Task Connection_string_configurada_para_base_fora_do_catalogo_sai_com_8()
    {
        var environment = await NewEnvironmentAsync();
        var ghost = NewName("fantasma");
        StagingTestSupport.ConfigureDatabase(environment.Settings, ghost,
            await _fixture.CreateMigratedTenantDatabaseAsync(NewName("vm_d")));   // acessível, mas o catálogo não a conhece
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings, output);

        Assert.Equal(ExitCodes.CannotVerify, code);
        Assert.Contains(ghost, output.ToString());
    }

    [Fact]
    public async Task Com_7_e_8_na_mesma_execucao_sai_com_7_e_a_saida_lista_as_duas_coisas()
    {
        var environment = await NewEnvironmentAsync();
        var dirty = NewName("suja");
        var unreachable = NewName("fora");
        await AddAsync(environment, dirty, await StagingTestSupport.ContaminatedDatabaseAsync(_fixture, NewName("vc_d")));
        await StagingTestSupport.RegisterDatabaseAsync(_fixture, environment.Catalog.Migrator, unreachable);
        environment.Settings[$"TenantDatabases:{unreachable}:MigrationConnectionString"] = ClosedPort;
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings, output);

        Assert.Equal(ExitCodes.FixedAdminFound, code);
        Assert.Contains(dirty, output.ToString());
        Assert.Contains(unreachable, output.ToString());
    }

    #endregion

    #region 8. Development

    [Fact]
    public async Task Recusa_com_6_em_Development_mesmo_com_a_configuracao_invalida()
    {
        var environment = await NewEnvironmentAsync("Development");
        environment.Settings["ConnectionStrings:CatalogMigration"] = null;   // configuração inválida...
        var output = new StringWriter();

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings, output);

        Assert.Equal(ExitCodes.VerifyRefusedInDevelopment, code);   // ...e ainda assim é 6, não 2
        Assert.Contains("Development", output.ToString());
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task Roda_em_Production_e_em_Staging(string environmentName)
    {
        var environment = await NewEnvironmentAsync(environmentName);

        Assert.Equal(ExitCodes.Ok, await StagingTestSupport.RunAsync(Verb, environment.Settings));
    }

    #endregion

    #region 9. Somente leitura

    [Fact]
    public async Task Nao_altera_nenhuma_base_nem_o_catalogo()
    {
        var environment = await NewEnvironmentAsync();
        var database = await StagingTestSupport.ContaminatedDatabaseAsync(_fixture, NewName("vc_e"));
        await AddAsync(environment, NewName("suja"), database);
        var before = await StagingTestSupport.ReadOperatorsAsync(database.Migrator);

        var code = await StagingTestSupport.RunAsync(Verb, environment.Settings);

        Assert.Equal(ExitCodes.FixedAdminFound, code);
        Assert.Equal(before, await StagingTestSupport.ReadOperatorsAsync(database.Migrator));   // contagem e hashes iguais
    }

    [Theory]
    [InlineData("create table nao_pode (id int)")]
    [InlineData("insert into operators (id, email, password, role, tenant_id) values (gen_random_uuid(), 'x@y.test', 'h', 1, gen_random_uuid())")]
    [InlineData("update operators set email = email")]
    public async Task A_conexao_somente_leitura_recusa_CREATE_INSERT_e_UPDATE_com_o_sqlstate_25006(string sql)
    {
        var database = await _fixture.CreateMigratedTenantDatabaseAsync(NewName("vm_e"));

        // Depois de um erro a transação fica abortada: por isso cada comando proibido usa uma conexão nova.
        await using var connection = await FixedAdminVerifier.OpenReadOnlyAsync(database.Migrator);
        await using var command = new NpgsqlCommand(sql, connection);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());

        Assert.Equal("25006", exception.SqlState);
    }

    // Controle: a mesma conexão LÊ normalmente (a recusa acima é da escrita, não de uma conexão quebrada).
    [Fact]
    public async Task A_conexao_somente_leitura_continua_lendo()
    {
        var database = await _fixture.CreateMigratedTenantDatabaseAsync(NewName("vm_f"));

        await using var connection = await FixedAdminVerifier.OpenReadOnlyAsync(database.Migrator);
        await using var command = new NpgsqlCommand("select count(*) from operators", connection);

        Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
    }

    #endregion

    #region 10. Ponta a ponta

    [Fact]
    public async Task Depois_do_seed_staging_a_verificacao_aprova_com_0()
    {
        var environment = await StagingTestSupport.NewEnvironmentAsync(_fixture);

        Assert.Equal(ExitCodes.Ok, await StagingTestSupport.RunAsync("seed-staging", environment));
        var output = new StringWriter();
        var code = await StagingTestSupport.RunAsync(Verb, environment, output);

        Assert.Equal(ExitCodes.Ok, code);
        Assert.Contains("Bases de tenant verificadas: 2", output.ToString());
    }

    #endregion
}
