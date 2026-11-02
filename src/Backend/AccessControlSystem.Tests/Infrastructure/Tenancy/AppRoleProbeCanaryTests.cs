using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// FR-023: a prova de privilégios do acs_app só vale se a sonda ACUSA um privilégio a mais. Cada canário é um papel
// descartável (nome único, NOLOGIN, removido ao final) criado em banco isolado com as migrations reais, e a sonda tem de
// devolver exatamente a violação do privilégio concedido. Contrato: contracts/app-role-guarantees.md.
[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public class AppRoleProbeCanaryTests
{
    private readonly PostgresContainerFixture _fixture;

    public AppRoleProbeCanaryTests(PostgresContainerFixture fixture) => _fixture = fixture;

    private async Task<long> CountRolesAsync(string name)
    {
        await using var connection = await _fixture.OpenSuperuserConnectionAsync();
        await using var command = new NpgsqlCommand("select count(*) from pg_roles where rolname = @name", connection);
        command.Parameters.AddWithValue("name", name);
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    // Cria o banco isolado (tenant ou catálogo) com as migrations reais, o canário e aplica o privilégio indevido; depois
    // do DisposeAsync confere que o papel do cluster sumiu (regra 11) e só então devolve as violações para a asserção.
    private async Task<IReadOnlyList<string>> RunAsync(
        string kind, ProbeProfile profile, Func<CanaryRole, DatabaseConnectionStrings, Task>? grant)
    {
        var database = profile == ProbeProfile.Catalog
            ? await _fixture.CreateIsolatedCatalogAsync()
            : await _fixture.CreateMigratedTenantDatabaseAsync($"cnr_{Guid.NewGuid():N}");

        string name;
        IReadOnlyList<string> violations;
        await using (var canary = await CanaryRole.CreateAsync(_fixture, kind))
        {
            name = canary.Name;
            if (grant is not null) await grant(canary, database);
            violations = await AppRoleProbe.ListViolationsAsync(database.Migrator, canary.Name, profile);
        }

        Assert.Equal(0L, await CountRolesAsync(name));   // o papel do cluster foi removido
        return violations;
    }

    [Fact]
    public async Task Canario_sem_privilegio_algum_nao_gera_violacao_em_base_de_tenant()
    {
        Assert.Empty(await RunAsync("clean", ProbeProfile.Tenant, grant: null));
    }

    [Fact]
    public async Task Canario_sem_privilegio_algum_nao_gera_violacao_no_catalogo()
    {
        Assert.Empty(await RunAsync("clean", ProbeProfile.Catalog, grant: null));
    }

    [Fact]
    public async Task Canario_com_create_no_esquema_gera_so_P8()
    {
        var violations = await RunAsync("ddl", ProbeProfile.Tenant,
            (canary, db) => canary.ExecuteInAsync(db.DatabaseName, "GRANT CREATE ON SCHEMA public TO {role}"));

        Assert.Equal([AppRoleProbe.CreateOnSchema], violations);
    }

    [Fact]
    public async Task Canario_com_delete_e_truncate_gera_so_P3_e_nunca_P9()
    {
        var violations = await RunAsync("delete", ProbeProfile.Tenant,
            (canary, db) => canary.ExecuteInAsync(db.DatabaseName, "GRANT DELETE, TRUNCATE ON operators TO {role}"));

        Assert.Equal([AppRoleProbe.DeleteOrTruncate], violations);
    }

    [Fact]
    public async Task Canario_com_references_e_trigger_gera_so_P9()
    {
        var violations = await RunAsync("extra", ProbeProfile.Tenant,
            (canary, db) => canary.ExecuteInAsync(db.DatabaseName, "GRANT REFERENCES, TRIGGER ON operators TO {role}"));

        Assert.Equal([AppRoleProbe.ExtraTablePrivilege], violations);
    }

    [Fact]
    public async Task Canario_com_bypassrls_gera_so_P6()
    {
        var violations = await RunAsync("bypass", ProbeProfile.Tenant,
            (canary, _) => canary.ExecuteInClusterAsync("ALTER ROLE {role} BYPASSRLS"));

        Assert.Equal([AppRoleProbe.BypassRls], violations);
    }

    // Dono da tabela tem todos os privilégios dela, então a sonda também acusa P3 e P9: o que se exige é a violação P7.
    [Fact]
    public async Task Canario_dono_de_tabela_gera_P7()
    {
        var violations = await RunAsync("owner", ProbeProfile.Tenant,
            (canary, db) => canary.ExecuteInAsync(db.DatabaseName, "ALTER TABLE operators OWNER TO {role}"));

        Assert.Contains(AppRoleProbe.OwnsTable, violations);
    }

    // Quem é membro do migrator herda os privilégios dele, então a sonda acusa outras violações junto: exige-se a P7.
    [Fact]
    public async Task Canario_membro_do_migrator_gera_P7()
    {
        var violations = await RunAsync("member", ProbeProfile.Tenant,
            (canary, _) => canary.ExecuteInClusterAsync($"GRANT {PostgresRoleBootstrap.MigratorRole} TO {{role}}"));

        Assert.Contains(AppRoleProbe.MemberOfMigrator, violations);
    }

    [Fact]
    public async Task Canario_com_escrita_no_catalogo_gera_so_P10_e_nunca_P9()
    {
        var violations = await RunAsync("catalog_write", ProbeProfile.Catalog,
            (canary, db) => canary.ExecuteInAsync(db.DatabaseName, "GRANT INSERT, UPDATE ON tenants TO {role}"));

        Assert.Equal([AppRoleProbe.CatalogWrite], violations);
    }

    // Um nome de papel errado não pode virar "lista vazia = tudo certo": a sonda tem de recusar o papel que não existe.
    [Fact]
    public async Task Papel_inexistente_e_recusado_em_vez_de_virar_lista_vazia()
    {
        var database = await _fixture.CreateMigratedTenantDatabaseAsync($"cnr_{Guid.NewGuid():N}");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => AppRoleProbe.ListViolationsAsync(database.Migrator, $"nao_existe_{Guid.NewGuid():N}", ProbeProfile.Tenant));
    }
}
