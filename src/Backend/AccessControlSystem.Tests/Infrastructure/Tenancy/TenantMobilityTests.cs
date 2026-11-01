using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Migrator.Orchestration;
using AccessControlSystem.Tests.Api.Support;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Net.Http.Json;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// História 7 (FR-032, FR-033): mover um tenant entre bases é COPIAR as linhas dele, sem transformar nenhum
// dado, e apontar o catálogo para a nova base. A propriedade vem do modelo (ids gerados pela aplicação e
// tenant_id como único vínculo); o procedimento operacional completo é de outra frente. Estes testes
// usam apenas SQL cru de cópia e a atualização de tenants.database_id.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class TenantMobilityTests : ApiTestBase
{
    private const string RowSnapshotSql =
        "select concat_ws('|', id, email, password, role, is_active, tenant_id) from operators where tenant_id = '{0}' order by id";

    public TenantMobilityTests(PostgresContainerFixture database) : base(database)
    {
    }

    #region Apoio

    /// <summary>Base dedicada nova, vazia e já com a estrutura (como o Migrator a deixaria).</summary>
    private async Task<DatabaseConnectionStrings> NewMigratedDatabaseAsync()
    {
        var database = await Database.CreateEmptyDatabaseAsync($"mover_{Guid.NewGuid():N}");
        var result = await new EfDatabaseMigrationRunner().RunAsync(database.Migrator, CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);
        return database;
    }

    /// <summary>
    /// Copia as linhas do tenant de uma base para outra, coluna a coluna e valor a valor, SEM nenhuma
    /// transformação (mesmos ids, mesmos hashes, mesmo tenant_id). Usa o migrator, que enxerga todos os tenants.
    /// </summary>
    private static async Task CopyTenantRowsAsync(string sourceMigrator, string targetMigrator, Guid tenantId)
    {
        await using var source = new NpgsqlConnection(sourceMigrator);
        await using var target = new NpgsqlConnection(targetMigrator);
        await source.OpenAsync();
        await target.OpenAsync();

        await using var read = new NpgsqlCommand("select * from operators where tenant_id = @tenant order by id", source);
        read.Parameters.AddWithValue("tenant", tenantId);
        await using var reader = await read.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var columns = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToArray();
            await using var insert = new NpgsqlCommand(
                $"insert into operators ({string.Join(", ", columns)}) values ({string.Join(", ", columns.Select((_, i) => $"@p{i}"))})",
                target);
            for (var i = 0; i < columns.Length; i++)
                insert.Parameters.AddWithValue($"p{i}", reader.GetValue(i));
            await insert.ExecuteNonQueryAsync();
        }
    }

    private static async Task DeleteTenantRowsAsync(string migratorConnectionString, Guid tenantId)
    {
        await using var connection = new NpgsqlConnection(migratorConnectionString);
        await connection.OpenAsync();
        await using var delete = new NpgsqlCommand("delete from operators where tenant_id = @tenant", connection);
        delete.Parameters.AddWithValue("tenant", tenantId);
        await delete.ExecuteNonQueryAsync();
    }

    /// <summary>Aponta o tenant para outra base no catálogo (a "mudança de base" é só esta atualização).</summary>
    private async Task<string> PointTenantToNewDatabaseAsync(Guid tenantId, EDatabaseKind kind)
    {
        var name = $"movida_{Guid.NewGuid():N}";
        await using var catalog = Database.CreateCatalogContext(asMigrator: true);
        var databaseId = Guid.NewGuid();
        catalog.Databases.Add(new CatalogDatabase { Id = databaseId, Name = name, Kind = kind, CreatedAt = DateTime.UtcNow });
        await catalog.SaveChangesAsync();
        await catalog.Tenants.Where(x => x.Id == tenantId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DatabaseId, databaseId));
        return name;
    }

    private static async Task<List<string>> SnapshotAsync(string migratorConnectionString, Guid tenantId) =>
        await DatabaseProbe.ReadStringsAsync(migratorConnectionString, string.Format(RowSnapshotSql, tenantId));

    private async Task<(string Token, HashSet<Guid> Ids)> LoginAndListAsync(ApiFactory factory, string email)
    {
        using var client = factory.CreateClient();

        var login = await client.PostAsJsonAsync("v1/account/login", new { email, password = OperatorPassword });
        Assert.Equal(200, (int)login.StatusCode);
        var token = System.Text.Json.JsonDocument.Parse(await login.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("token").GetString()!;

        var list = await client.SendAsync(TestTokens.Authorized(HttpMethod.Get, "v1/account/get-list?size=50", token));
        Assert.Equal(200, (int)list.StatusCode);
        var ids = System.Text.Json.JsonDocument.Parse(await list.Content.ReadAsStringAsync())
            .RootElement.GetProperty("data").GetProperty("operators").EnumerateArray()
            .Select(o => o.GetProperty("id").GetGuid()).ToHashSet();
        return (token, ids);
    }

    #endregion

    [Fact]
    public async Task Tenant_copiado_para_base_dedicada_funciona_de_forma_identica()
    {
        var tenantId = await ApiFactory.RegisterTenantAsync(Database);
        var admin = await Database.SeedOperatorAsync(tenantId, role: ERole.Admin);
        var colleague = await Database.SeedOperatorAsync(tenantId);
        var inactive = await Database.SeedOperatorAsync(tenantId, isActive: false);

        // Antes: o tenant funciona na base compartilhada.
        using var beforeFactory = new ApiFactory(Database).WithDefaultTenant(tenantId);
        var (_, idsBefore) = await LoginAndListAsync(beforeFactory, admin.Email);
        var rowsBefore = await SnapshotAsync(Database.SharedMigratorConnectionString, tenantId);

        // Mover = copiar as linhas, sem transformar, para uma base dedicada vazia + apontar o catálogo.
        var dedicated = await NewMigratedDatabaseAsync();
        await CopyTenantRowsAsync(Database.SharedMigratorConnectionString, dedicated.Migrator, tenantId);
        await DeleteTenantRowsAsync(Database.SharedMigratorConnectionString, tenantId);
        var newName = await PointTenantToNewDatabaseAsync(tenantId, EDatabaseKind.Dedicated);

        // As linhas na base nova são exatamente as de antes (mesmos ids, hashes e tenant_id).
        Assert.Equal(rowsBefore, await SnapshotAsync(dedicated.Migrator, tenantId));
        Assert.Empty(await SnapshotAsync(Database.SharedMigratorConnectionString, tenantId));

        // Depois: o mesmo login e a mesma listagem, agora lidos da base dedicada, com os MESMOS ids.
        Factory.WithTenantDatabase(newName, dedicated.App).WithDefaultTenant(tenantId);
        var (token, idsAfter) = await LoginAndListAsync(Factory, admin.Email);
        Assert.Equal(idsBefore, idsAfter);
        Assert.Contains(colleague.Id, idsAfter);
        Assert.Contains(inactive.Id, idsAfter);

        // Alteração: grava na base nova (e só nela).
        var novoEmail = TenantSeed.UniqueEmail("movido");
        var update = await SendAsync(HttpMethod.Put, $"v1/account/update/{colleague.Id}", token, new { email = novoEmail });
        Assert.Equal(200, (int)update.StatusCode);
        Assert.Equal(1L, await DatabaseProbe.CountAsync(dedicated.Migrator, $"select count(*) from operators where id = '{colleague.Id}' and email = '{novoEmail}'"));
        Assert.Equal(0L, await DatabaseProbe.CountAsync(Database.SharedMigratorConnectionString, $"select count(*) from operators where id = '{colleague.Id}'"));
    }

    [Fact]
    public async Task Outros_tenants_da_base_de_origem_nao_percebem_a_mudanca()
    {
        var movedTenant = await ApiFactory.RegisterTenantAsync(Database);
        var stayingTenant = await ApiFactory.RegisterTenantAsync(Database);
        var email = TenantSeed.UniqueEmail("igual");   // o mesmo e-mail nos dois tenants
        await Database.SeedOperatorAsync(movedTenant, email, ERole.Admin);
        var stayingAdmin = await Database.SeedOperatorAsync(stayingTenant, email, ERole.Admin);
        await Database.SeedOperatorAsync(stayingTenant);

        using var beforeFactory = new ApiFactory(Database).WithDefaultTenant(stayingTenant);
        var (_, stayingIdsBefore) = await LoginAndListAsync(beforeFactory, email);
        var stayingRowsBefore = await SnapshotAsync(Database.SharedMigratorConnectionString, stayingTenant);

        var dedicated = await NewMigratedDatabaseAsync();
        await CopyTenantRowsAsync(Database.SharedMigratorConnectionString, dedicated.Migrator, movedTenant);
        await DeleteTenantRowsAsync(Database.SharedMigratorConnectionString, movedTenant);
        await PointTenantToNewDatabaseAsync(movedTenant, EDatabaseKind.Dedicated);

        // O tenant que ficou: linhas idênticas, mesmo login, mesma listagem, mesmo token de sempre.
        Assert.Equal(stayingRowsBefore, await SnapshotAsync(Database.SharedMigratorConnectionString, stayingTenant));
        Factory.WithDefaultTenant(stayingTenant);
        var (token, stayingIdsAfter) = await LoginAndListAsync(Factory, email);
        Assert.Equal(stayingIdsBefore, stayingIdsAfter);
        var me = await GetAsync("v1/account/get-me", TestTokens.For(stayingAdmin, stayingTenant));
        Assert.Equal(200, (int)me.StatusCode);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task Tenant_movido_de_volta_para_base_com_outros_tenants_nao_gera_conflito_de_id()
    {
        // A base compartilhada já tem outros tenants, e um deles tem o MESMO e-mail do tenant que vai e volta.
        var travelling = await ApiFactory.RegisterTenantAsync(Database);
        var neighbour = await ApiFactory.RegisterTenantAsync(Database);
        var email = TenantSeed.UniqueEmail("viajante");
        var admin = await Database.SeedOperatorAsync(travelling, email, ERole.Admin);
        var other = await Database.SeedOperatorAsync(travelling);
        await Database.SeedOperatorAsync(neighbour, email, ERole.Admin);
        var travellingRows = await SnapshotAsync(Database.SharedMigratorConnectionString, travelling);
        var neighbourRows = await SnapshotAsync(Database.SharedMigratorConnectionString, neighbour);

        // Ida: compartilhada → dedicada.
        var dedicated = await NewMigratedDatabaseAsync();
        await CopyTenantRowsAsync(Database.SharedMigratorConnectionString, dedicated.Migrator, travelling);
        await DeleteTenantRowsAsync(Database.SharedMigratorConnectionString, travelling);
        Assert.Equal(travellingRows, await SnapshotAsync(dedicated.Migrator, travelling));

        // Volta: dedicada → a MESMA base compartilhada, que ainda tem o vizinho com o mesmo e-mail.
        await CopyTenantRowsAsync(dedicated.Migrator, Database.SharedMigratorConnectionString, travelling);
        await DeleteTenantRowsAsync(dedicated.Migrator, travelling);

        // Sem conflito de id nem de e-mail (a unicidade é por tenant), e nada do vizinho mudou.
        Assert.Equal(travellingRows, await SnapshotAsync(Database.SharedMigratorConnectionString, travelling));
        Assert.Equal(neighbourRows, await SnapshotAsync(Database.SharedMigratorConnectionString, neighbour));

        // O tenant volta a funcionar na base compartilhada com os mesmos ids.
        await using var context = Database.CreateContext(travelling);
        Assert.Equal(
            new[] { admin.Id, other.Id }.OrderBy(x => x),
            (await context.Operators.AsNoTracking().Select(x => x.Id).ToListAsync()).OrderBy(x => x));
    }
}
