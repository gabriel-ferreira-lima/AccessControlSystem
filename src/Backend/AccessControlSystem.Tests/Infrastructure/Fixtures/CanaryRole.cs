using System.Text.RegularExpressions;
using Npgsql;

namespace AccessControlSystem.Tests.Infrastructure.Fixtures;

/// <summary>
/// Papel de banco descartável, criado de propósito com um privilégio a mais para provar que a sonda
/// (<see cref="AppRoleProbe"/>) acusa a violação (FR-023). Papéis pertencem ao cluster inteiro, não ao banco isolado em
/// que recebem o privilégio, então:
/// <list type="bullet">
/// <item>o nome é único por execução (<c>canary_&lt;tipo&gt;_&lt;Guid:N&gt;</c>);</item>
/// <item>o papel é <c>NOLOGIN</c> (a sonda consulta o catálogo pela conexão do migrator, ninguém precisa logar com ele);</item>
/// <item>o <see cref="DisposeAsync"/> remove o papel mesmo se a asserção do teste falhar: antes do <c>DROP ROLE</c>, roda
/// <c>REASSIGN OWNED</c> e <c>DROP OWNED</c> em cada banco onde ele recebeu algo (sem isso o <c>DROP ROLE</c> falha com
/// "role cannot be dropped because some objects depend on it").</item>
/// </list>
/// Nenhum canário toca o <c>acs_app</c> nem o <c>acs_migrator</c>. Uso: <c>await using var canario = await CanaryRole.CreateAsync(...)</c>.
/// </summary>
public sealed partial class CanaryRole : IAsyncDisposable
{
    private readonly PostgresContainerFixture _fixture;
    private readonly HashSet<string> _databases = [];

    private CanaryRole(PostgresContainerFixture fixture, string name)
    {
        _fixture = fixture;
        Name = name;
    }

    /// <summary>Nome único do papel no cluster.</summary>
    public string Name { get; }

    public static async Task<CanaryRole> CreateAsync(PostgresContainerFixture fixture, string kind)
    {
        if (!KindPattern().IsMatch(kind))
            throw new ArgumentException("Tipo de canário inválido.", nameof(kind));

        var role = new CanaryRole(fixture, $"canary_{kind}_{Guid.NewGuid():N}");

        await using var connection = await fixture.OpenSuperuserConnectionAsync();
        await ExecuteAsync(connection, $"CREATE ROLE {role.Quoted} NOLOGIN");

        return role;
    }

    /// <summary>Como o nome vem da própria classe (só letras minúsculas, dígitos e sublinhado), o identificador entre aspas é seguro.</summary>
    private string Quoted => $"\"{Name}\"";

    /// <summary>
    /// Executa um SQL como superusuário no banco isolado e registra o banco para a limpeza. <c>{role}</c> no texto vira o
    /// identificador do canário.
    /// </summary>
    public async Task ExecuteInAsync(string database, string sql)
    {
        _databases.Add(database);

        await using var connection = await _fixture.OpenSuperuserConnectionAsync(database);
        await ExecuteAsync(connection, sql.Replace("{role}", Quoted, StringComparison.Ordinal));
    }

    /// <summary>Executa um SQL como superusuário no cluster (atributos e associações do papel). <c>{role}</c> vira o canário.</summary>
    public async Task ExecuteInClusterAsync(string sql)
    {
        await using var connection = await _fixture.OpenSuperuserConnectionAsync();
        await ExecuteAsync(connection, sql.Replace("{role}", Quoted, StringComparison.Ordinal));
    }

    public async ValueTask DisposeAsync()
    {
        Exception? first = null;

        foreach (var database in _databases)
        {
            try
            {
                await using var connection = await _fixture.OpenSuperuserConnectionAsync(database);
                await ExecuteAsync(connection, $"REASSIGN OWNED BY {Quoted} TO {PostgresRoleBootstrap.MigratorRole}");
                await ExecuteAsync(connection, $"DROP OWNED BY {Quoted}");
            }
            catch (Exception exception)
            {
                first ??= exception;   // segue para o DROP ROLE; o erro é reportado no fim
            }
        }

        try
        {
            await using var connection = await _fixture.OpenSuperuserConnectionAsync();
            await ExecuteAsync(connection, $"DROP ROLE IF EXISTS {Quoted}");
        }
        catch (Exception exception)
        {
            first ??= exception;
        }

        if (first is not null)
            throw new InvalidOperationException($"Falha ao remover o papel-canário {Name}.", first);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    [GeneratedRegex("^[a-z][a-z_]{0,20}$")]
    private static partial Regex KindPattern();
}
