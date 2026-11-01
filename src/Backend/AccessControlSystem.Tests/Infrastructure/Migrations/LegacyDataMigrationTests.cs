using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using System.IdentityModel.Tokens.Jwt;

namespace AccessControlSystem.Tests.Infrastructure.Migrations;

// Adoção de uma base que já existia antes do multi-tenant (FR-027 a FR-029): as linhas legadas passam a
// pertencer ao tenant inicial, sem perda e sem alterar nenhum outro campo.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class LegacyDataMigrationTests : ApiTestBase
{
    // Última migration do formato legado (v1 + SeedAdmin + AddIsActiveToOperator).
    private const string LastLegacyMigration = "20260910225125_AddIsActiveToOperator";
    private const string FirstTenantMigration = "20261101141816_AddTenantIdToOperators";
    private const string EnforceMigration = "20261101141854_EnforceTenantIsolationOnOperators";
    private static readonly Guid SeedAdminId = Guid.Parse("cc59e349-efbd-4800-9726-f38a325e4d3d");

    private const string RowSnapshotSql =
        "select concat_ws('|', id, email, password, role, is_active) from operators order by id";

    public LegacyDataMigrationTests(PostgresContainerFixture database) : base(database)
    {
    }

    #region Base legada

    /// <summary>
    /// Base no formato de antes desta feature: migrada só até a última migration legada (com o
    /// administrador de seed que as bases antigas têm) e com operadores inseridos direto na tabela.
    /// </summary>
    private async Task<(DatabaseConnectionStrings Database, string Email, string PasswordHash)> CreateLegacyDatabaseAsync()
    {
        var database = await Database.CreateEmptyDatabaseAsync($"legacy_{Guid.NewGuid():N}");

        await using (var context = Database.CreateMigrationContext(database.Migrator))
        {
            await context.GetService<IMigrator>().MigrateAsync(LastLegacyMigration);
        }

        var hash = new Password(OperatorPassword).Hash;
        var email = $"legado-{Guid.NewGuid():N}@test.com";
        await InsertLegacyOperatorAsync(database.Migrator, Guid.NewGuid(), email, hash, ERole.Operator, isActive: true);
        await InsertLegacyOperatorAsync(database.Migrator, Guid.NewGuid(), $"legado-adm-{Guid.NewGuid():N}@test.com", hash, ERole.Admin, isActive: true);
        await InsertLegacyOperatorAsync(database.Migrator, Guid.NewGuid(), $"legado-off-{Guid.NewGuid():N}@test.com", hash, ERole.Operator, isActive: false);

        return (database, email, hash);
    }

    private static async Task InsertLegacyOperatorAsync(
        string connectionString, Guid id, string email, string passwordHash, ERole role, bool isActive)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "insert into operators (id, email, password, role, is_active) values (@id, @email, @password, @role, @active)",
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("email", email);
        command.Parameters.AddWithValue("password", passwordHash);
        command.Parameters.AddWithValue("role", (short)role);
        command.Parameters.AddWithValue("active", isActive);
        await command.ExecuteNonQueryAsync();
    }

    #endregion

    [Fact]
    public async Task Dados_legados_passam_a_pertencer_ao_tenant_inicial_sem_perda()
    {
        var (database, _, _) = await CreateLegacyDatabaseAsync();
        var initialTenant = Guid.NewGuid();
        var before = await DatabaseProbe.ReadStringsAsync(database.Migrator, RowSnapshotSql);
        Assert.Equal(4, before.Count);   // o admin de seed e os três operadores inseridos

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None, initialTenant);

        Assert.True(result.Succeeded, result.Error);
        var after = await DatabaseProbe.ReadStringsAsync(database.Migrator, RowSnapshotSql);
        Assert.Equal(before, after);   // mesma contagem e nenhum campo alterado além de tenant_id

        Assert.Equal(4L, await DatabaseProbe.CountAsync(
            database.Migrator, $"select count(*) from operators where tenant_id = '{initialTenant}'"));
        Assert.Equal(0L, await DatabaseProbe.CountAsync(
            database.Migrator, "select count(*) from operators where tenant_id is null"));
    }

    [Fact]
    public async Task Operador_legado_faz_login_com_as_credenciais_de_antes()
    {
        var (database, email, _) = await CreateLegacyDatabaseAsync();

        // O tenant inicial é cadastrado no catálogo apontando para a base legada.
        var databaseName = $"legado_{Guid.NewGuid():N}";
        var initialTenant = await ApiFactory.RegisterTenantAsync(Database, databaseName: databaseName);
        Factory.WithTenantDatabase(databaseName, database.App).WithDefaultTenant(initialTenant);

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None, initialTenant);
        Assert.True(result.Succeeded, result.Error);

        var envelope = await ReadAsync(await LoginAsync(email, OperatorPassword));

        Assert.Equal(200, envelope.HttpStatus);
        Assert.Equal(email, envelope.Data.GetProperty("email").GetString());
        var token = new JwtSecurityTokenHandler().ReadJwtToken(envelope.Data.GetProperty("token").GetString());
        Assert.Equal(initialTenant.ToString(), token.Claims.Single(c => c.Type == "tenant").Value);
    }

    [Fact]
    public async Task Sem_tenant_inicial_a_migration_falha_no_not_null_e_nao_atribui_tenant()
    {
        var (database, _, _) = await CreateLegacyDatabaseAsync();
        var before = await DatabaseProbe.ReadStringsAsync(database.Migrator, RowSnapshotSql);

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None);

        // Falha fechada: nunca atribui as linhas a um tenant adivinhado.
        Assert.False(result.Succeeded);
        Assert.Contains("tenant_id", result.Error!);
        Assert.Equal(0L, await DatabaseProbe.CountAsync(
            database.Migrator, "select count(*) from operators where tenant_id is not null"));
        Assert.Equal(before, await DatabaseProbe.ReadStringsAsync(database.Migrator, RowSnapshotSql));   // nada se perdeu

        // A segunda migration (a que impõe o isolamento) não ficou aplicada.
        var history = await DatabaseProbe.ReadHistoryAsync(database.Migrator);
        Assert.DoesNotContain(EnforceMigration, history);
    }

    [Fact]
    public async Task Depois_de_falhar_sem_tenant_inicial_a_segunda_execucao_com_tenant_conclui_sem_update_manual()
    {
        // Princípio XIII: o passo de migrations é retomável. A 1ª execução (sem tenant inicial) falha e
        // deixa a AddTenantIdToOperators aplicada com as linhas ainda sem tenant; a 2ª, já com o tenant,
        // precisa concluir sozinha, sem ninguém rodar um UPDATE à mão.
        var (database, _, _) = await CreateLegacyDatabaseAsync();
        var initialTenant = Guid.NewGuid();
        var before = await DatabaseProbe.ReadStringsAsync(database.Migrator, RowSnapshotSql);
        var runner = new EfDatabaseMigrationRunner();

        var first = await runner.RunAsync(database.Migrator, CancellationToken.None);
        Assert.False(first.Succeeded);
        var historyAfterFailure = await DatabaseProbe.ReadHistoryAsync(database.Migrator);
        Assert.Contains(FirstTenantMigration, historyAfterFailure);       // a 1ª migration nova já ficou aplicada
        Assert.DoesNotContain(EnforceMigration, historyAfterFailure);     // a 2ª foi desfeita

        var second = await runner.RunAsync(database.Migrator, CancellationToken.None, initialTenant);

        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(EnforceMigration, second.SchemaVersion);
        Assert.Equal(before, await DatabaseProbe.ReadStringsAsync(database.Migrator, RowSnapshotSql));
        Assert.Equal(4L, await DatabaseProbe.CountAsync(
            database.Migrator, $"select count(*) from operators where tenant_id = '{initialTenant}'"));
    }

    [Fact]
    public async Task Repetir_a_adocao_com_o_mesmo_tenant_nao_altera_nada()
    {
        var (database, _, _) = await CreateLegacyDatabaseAsync();
        var initialTenant = Guid.NewGuid();
        var runner = new EfDatabaseMigrationRunner();
        await runner.RunAsync(database.Migrator, CancellationToken.None, initialTenant);
        var snapshot = await DatabaseProbe.ReadStringsAsync(database.Migrator, RowSnapshotSql);

        // Outra execução, até com outro tenant informado, não toca em linhas que já têm dono.
        var again = await runner.RunAsync(database.Migrator, CancellationToken.None, Guid.NewGuid());

        Assert.True(again.Succeeded, again.Error);
        Assert.Equal(snapshot, await DatabaseProbe.ReadStringsAsync(database.Migrator, RowSnapshotSql));
        Assert.Equal(4L, await DatabaseProbe.CountAsync(
            database.Migrator, $"select count(*) from operators where tenant_id = '{initialTenant}'"));
    }

    [Fact]
    public async Task Admin_de_seed_legado_e_preservado_no_tenant_inicial()
    {
        var (database, _, _) = await CreateLegacyDatabaseAsync();
        var initialTenant = Guid.NewGuid();
        var seedBefore = await DatabaseProbe.ReadStringsAsync(
            database.Migrator, $"select concat_ws('|', id, email, password, role, is_active) from operators where id = '{SeedAdminId}'");
        Assert.Single(seedBefore);   // a base legada tem o administrador de seed

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None, initialTenant);

        Assert.True(result.Succeeded, result.Error);
        // Preservado (não é apagado) e passa a pertencer ao tenant inicial, com os mesmos dados de antes.
        var seedAfter = await DatabaseProbe.ReadStringsAsync(
            database.Migrator, $"select concat_ws('|', id, email, password, role, is_active) from operators where id = '{SeedAdminId}' and tenant_id = '{initialTenant}'");
        Assert.Equal(seedBefore, seedAfter);
        Assert.Equal(1L, await DatabaseProbe.CountAsync(
            database.Migrator, "select count(*) from operators where email = 'admin@admin.com.br'"));
    }

    [Fact]
    public async Task Com_tenant_inicial_a_base_nova_continua_sem_operadores()
    {
        var database = await Database.CreateEmptyDatabaseAsync($"nova_{Guid.NewGuid():N}");

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None, Guid.NewGuid());

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(0L, await DatabaseProbe.CountAsync(database.Migrator, "select count(*) from operators"));
    }

    [Fact]
    public async Task Tenant_inicial_vazio_e_recusado_e_nada_e_migrado()
    {
        var (database, _, _) = await CreateLegacyDatabaseAsync();

        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None, Guid.Empty);

        Assert.False(result.Succeeded);
        var history = await DatabaseProbe.ReadHistoryAsync(database.Migrator);
        Assert.DoesNotContain(FirstTenantMigration, history);
        Assert.Equal(LastLegacyMigration, history[^1]);
    }
}
