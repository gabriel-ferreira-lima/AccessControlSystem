using AccessControlSystem.Infrastructure.Tenancy;
using AccessControlSystem.Infrastructure.Tenancy.Options;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Tests.Infrastructure.Tenancy;

// O catálogo guarda só o nome da base; a connection string vem de TenantDatabases:{name}.
public class TenantDatabaseConnectionProviderTests
{
    private const string AppConnectionString = "Host=db;Database=shared_01;Username=acs_app;Password=senha-do-app-123";
    private const string MigrationConnectionString = "Host=db;Database=shared_01;Username=acs_migrator;Password=senha-do-migrator-456";

    private static TenantDatabaseConnectionProvider CreateProvider(TenantDatabasesOptions options) =>
        new(Options.Create(options));

    [Fact]
    public void Devolve_a_connection_string_da_base()
    {
        var provider = CreateProvider(new TenantDatabasesOptions
        {
            ["shared_01"] = new TenantDatabaseOptions { ConnectionString = AppConnectionString }
        });

        Assert.Equal(AppConnectionString, provider.GetConnectionString("shared_01"));
    }

    [Fact]
    public void Nome_da_base_nao_diferencia_maiusculas_de_minusculas()
    {
        var provider = CreateProvider(new TenantDatabasesOptions
        {
            ["shared_01"] = new TenantDatabaseOptions { ConnectionString = AppConnectionString }
        });

        Assert.Equal(AppConnectionString, provider.GetConnectionString("SHARED_01"));
    }

    [Fact]
    public void Lanca_quando_a_base_nao_esta_configurada()
    {
        var provider = CreateProvider(new TenantDatabasesOptions
        {
            ["shared_01"] = new TenantDatabaseOptions { ConnectionString = AppConnectionString }
        });

        var exception = Assert.Throws<InvalidOperationException>(() => provider.GetConnectionString("outra_base"));

        Assert.Contains("outra_base", exception.Message);   // o nome da base ajuda a operação; não é segredo
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Lanca_quando_a_base_esta_configurada_sem_connection_string(string? connectionString)
    {
        var provider = CreateProvider(new TenantDatabasesOptions
        {
            ["shared_01"] = new TenantDatabaseOptions { ConnectionString = connectionString }
        });

        Assert.Throws<InvalidOperationException>(() => provider.GetConnectionString("shared_01"));
    }

    [Fact]
    public void Mensagem_de_erro_nao_inclui_a_connection_string()
    {
        // A base tem só a connection string de migração (que a Api nunca deve usar) e a de outra base
        // tem senha: nenhuma delas pode aparecer na mensagem do erro.
        var provider = CreateProvider(new TenantDatabasesOptions
        {
            ["shared_01"] = new TenantDatabaseOptions { ConnectionString = null, MigrationConnectionString = MigrationConnectionString },
            ["dedicated_01"] = new TenantDatabaseOptions { ConnectionString = AppConnectionString }
        });

        var semConnectionString = Assert.Throws<InvalidOperationException>(() => provider.GetConnectionString("shared_01"));
        var naoConfigurada = Assert.Throws<InvalidOperationException>(() => provider.GetConnectionString("outra_base"));

        foreach (var message in new[] { semConnectionString.Message, naoConfigurada.Message })
        {
            Assert.DoesNotContain("senha-do-app-123", message);
            Assert.DoesNotContain("senha-do-migrator-456", message);
            Assert.DoesNotContain("Password", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Host=", message);
        }
    }

    [Fact]
    public void A_connection_string_de_migracao_nunca_e_usada_pela_api()
    {
        var provider = CreateProvider(new TenantDatabasesOptions
        {
            ["shared_01"] = new TenantDatabaseOptions { ConnectionString = null, MigrationConnectionString = MigrationConnectionString }
        });

        Assert.Throws<InvalidOperationException>(() => provider.GetConnectionString("shared_01"));
    }
}
