using System.Collections.Concurrent;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Catalog.Models;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AccessControlSystem.Tests.Api.Support;

/// <summary>
/// Sobe o pipeline real da Api (TestServer) apontado para as bases da <see cref="PostgresContainerFixture"/>:
/// catálogo, base compartilhada (<c>shared_01</c>) e base dedicada (<c>dedicated_01</c>), sempre com o
/// papel da aplicação (<c>acs_app</c>, sujeito ao RLS). A configuração vai em memória e tem precedência
/// sobre variáveis de ambiente, então o resultado não depende da máquina de quem roda.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Chave de teste com mais de 256 bits (a Api e os tokens de teste usam a mesma).</summary>
    public const string JwtKey = "chave-de-teste-da-api-com-mais-de-256-bits-bbbbbbbbbbbbbbbbbbbb";

    private readonly Dictionary<string, string?> _settings = new();

    public ApiFactory(PostgresContainerFixture database)
    {
        _settings["ConnectionStrings:Catalog"] = database.CatalogAppConnectionString;
        _settings[$"TenantDatabases:{PostgresContainerFixture.SharedCatalogDatabaseName}:ConnectionString"] = database.SharedAppConnectionString;
        _settings[$"TenantDatabases:{PostgresContainerFixture.DedicatedCatalogDatabaseName}:ConnectionString"] = database.DedicatedAppConnectionString;
        _settings["Secrets:JwtPrivateKey"] = JwtKey;
        _settings["Login:DefaultTenantId"] = null;
        _settings["HttpsRedirection:Enabled"] = "false";
    }

    /// <summary>
    /// Troca (ou remove, com <c>null</c>) uma chave de configuração. Só vale antes de a Api subir, ou
    /// seja, antes do primeiro <c>CreateClient()</c>.
    /// </summary>
    public ApiFactory With(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    /// <summary>Define o tenant padrão do login (<c>Login:DefaultTenantId</c>); <c>null</c> deixa a chave ausente.</summary>
    public ApiFactory WithDefaultTenant(Guid? tenantId) => With("Login:DefaultTenantId", tenantId?.ToString());

    /// <summary>Aponta uma base do catálogo para outra connection string (ex.: uma base legada ou inacessível).</summary>
    public ApiFactory WithTenantDatabase(string name, string connectionString) =>
        With($"TenantDatabases:{name}:ConnectionString", connectionString);

    private readonly ConcurrentQueue<CapturedLog> _logs = new();

    /// <summary>Tudo o que a Api registrou no log (categoria, nível e texto, com a exceção), para conferir o que foi (ou não) logado.</summary>
    public IReadOnlyCollection<CapturedLog> Logs => _logs;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" isola os testes de user-secrets e do appsettings.Development.json da máquina.
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_settings));
        builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(_logs)));
    }

    public sealed record CapturedLog(string Category, LogLevel Level, string Text);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<CapturedLog> _sink;

        public CapturingLoggerProvider(ConcurrentQueue<CapturedLog> sink) => _sink = sink;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _sink);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly string _category;
        private readonly ConcurrentQueue<CapturedLog> _sink;

        public CapturingLogger(string category, ConcurrentQueue<CapturedLog> sink)
        {
            _category = category;
            _sink = sink;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var text = formatter(state, exception);
            if (exception is not null) text += Environment.NewLine + exception;
            _sink.Enqueue(new CapturedLog(_category, logLevel, text));
        }
    }

    #region Tenants no catálogo

    /// <summary>
    /// Cria no catálogo um tenant com <c>Guid</c> e CNPJ únicos, apontando para a base compartilhada
    /// (padrão), para a dedicada ou, com <paramref name="databaseName"/>, para uma base nova com esse
    /// nome (útil para simular base sem connection string ou inacessível).
    /// </summary>
    public static async Task<Guid> RegisterTenantAsync(
        PostgresContainerFixture database,
        bool dedicated = false,
        bool isActive = true,
        string? databaseName = null)
    {
        await using var context = database.CreateCatalogContext(asMigrator: true);
        var now = DateTime.UtcNow;

        Guid databaseId;
        if (databaseName is not null)
        {
            databaseId = Guid.NewGuid();
            context.Databases.Add(new CatalogDatabase
            {
                Id = databaseId, Name = databaseName, Kind = EDatabaseKind.Shared, CreatedAt = now
            });
        }
        else
        {
            databaseId = dedicated
                ? PostgresContainerFixture.DedicatedCatalogDatabaseId
                : PostgresContainerFixture.SharedCatalogDatabaseId;
        }

        var tenantId = Guid.NewGuid();
        context.Tenants.Add(new CatalogTenant
        {
            Id = tenantId,
            Cnpj = Guid.NewGuid().ToString("N")[..18],
            Name = "Empresa de Teste",
            IsActive = isActive,
            DatabaseId = databaseId,
            CreatedAt = now
        });
        await context.SaveChangesAsync();

        return tenantId;
    }

    /// <summary>Ativa ou inativa um tenant já cadastrado (o catálogo é escrito pelo papel migrator).</summary>
    public static async Task SetTenantActiveAsync(PostgresContainerFixture database, Guid tenantId, bool isActive)
    {
        await using var context = database.CreateCatalogContext(asMigrator: true);
        await context.Tenants
            .Where(x => x.Id == tenantId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.IsActive, isActive));
    }

    #endregion
}
