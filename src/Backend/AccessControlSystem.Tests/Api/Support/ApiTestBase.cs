using System.Net.Http.Json;
using System.Text.Json;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Tests.Infrastructure.Fixtures;
using AccessControlSystem.Tests.Infrastructure.Tenancy;

namespace AccessControlSystem.Tests.Api.Support;

/// <summary>
/// Base dos testes que sobem a Api: cada teste tem a sua própria <see cref="ApiFactory"/> (e, portanto,
/// o seu limitador de taxa e a sua configuração), criada sem subir nada até o primeiro
/// <see cref="Client"/>. Assim o teste ainda pode trocar a configuração antes de a Api iniciar.
/// </summary>
public abstract class ApiTestBase : IDisposable
{
    /// <summary>Senha de todo operador criado por <c>TenantSeed</c>.</summary>
    protected const string OperatorPassword = "Str0ng!Pass";

    private HttpClient? _client;

    protected ApiTestBase(PostgresContainerFixture database)
    {
        Database = database;
        Factory = new ApiFactory(database);
    }

    protected PostgresContainerFixture Database { get; }

    protected ApiFactory Factory { get; }

    protected HttpClient Client => _client ??= Factory.CreateClient();

    public void Dispose()
    {
        _client?.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Dados

    /// <summary>Tenant novo no catálogo com um operador (admin por padrão) na base correspondente.</summary>
    protected async Task<(Guid TenantId, Operator Operator)> TenantWithOperatorAsync(
        ERole role = ERole.Admin, bool dedicated = false, bool isActive = true)
    {
        var tenantId = await ApiFactory.RegisterTenantAsync(Database, dedicated, isActive);
        var @operator = await Database.SeedOperatorAsync(tenantId, role: role, dedicated: dedicated);
        return (tenantId, @operator);
    }

    #endregion

    #region Requisições

    protected Task<HttpResponseMessage> GetAsync(string uri, string? token = null) =>
        SendAsync(HttpMethod.Get, uri, token);

    protected Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string? token, object? body = null)
    {
        var content = body is null ? null : JsonContent.Create(body);
        var request = token is null
            ? new HttpRequestMessage(method, uri) { Content = content }
            : TestTokens.Authorized(method, uri, token, content);

        return Client.SendAsync(request);
    }

    protected Task<HttpResponseMessage> LoginAsync(string email, string password, string? bearerToken = null) =>
        SendAsync(HttpMethod.Post, "v1/account/login", bearerToken, new { email, password });

    #endregion

    #region Leitura da resposta

    protected static async Task<Envelope> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return new Envelope((int)response.StatusCode, text);
    }

    /// <summary>O corpo JSON de uma resposta da Api (envelope <c>message</c>/<c>status</c>/<c>data</c>).</summary>
    protected sealed class Envelope
    {
        private readonly JsonDocument? _document;

        public Envelope(int httpStatus, string body)
        {
            HttpStatus = httpStatus;
            Body = body;
            if (!string.IsNullOrWhiteSpace(body) && body.TrimStart().StartsWith('{'))
                _document = JsonDocument.Parse(body);
        }

        public int HttpStatus { get; }

        public string Body { get; }

        public JsonElement Root => _document?.RootElement ?? throw new InvalidOperationException($"Corpo sem JSON de objeto: '{Body}'");

        public string? Message => Root.GetProperty("message").GetString();

        public int Status => Root.GetProperty("status").GetInt32();

        public bool HasData => Root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object;

        public JsonElement Data => Root.GetProperty("data");

        public IReadOnlyCollection<string> PropertyNames => Names(Root);

        public IReadOnlyCollection<string> DataPropertyNames => Names(Data);

        private static string[] Names(JsonElement element) =>
            element.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }

    #endregion
}
