using System.Net.Sockets;
using Npgsql;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>Resultado de uma verificação: limpa, contaminada (achou o administrador fixo) ou não verificável.</summary>
    public enum DatabaseVerdict {
        Clean,
        Contaminated,
        Unverifiable
    }

    /// <summary>
    /// Resultado de UMA base (ou do catálogo): o nome lógico e um motivo curto. O motivo nunca traz connection string,
    /// e-mail, identificador de operador nem senha.
    /// </summary>
    public sealed record DatabaseVerification(string Name, DatabaseVerdict Verdict, string? Reason);

    /// <summary>
    /// Procura o administrador fixo da migration <c>SeedAdmin</c> numa base de tenant ou no catálogo, SÓ LENDO: cada
    /// conexão abre uma transação somente leitura (<c>BEGIN READ ONLY</c>), então escrever falharia com o SQLSTATE
    /// <c>25006</c>. Os dados fixos vêm do <see cref="SeedAdminFingerprint"/>, nunca de literais duplicados.
    /// </summary>
    /// <remarks>
    /// Basta UM dado fixo para reprovar: o id <b>ou</b> o e-mail (sem diferenciar maiúsculas de minúsculas). Base sem a
    /// tabela de operadores conta como "não contém". Qualquer erro (rede, credencial, consulta) é "não verificável":
    /// nunca "limpa". Usa a connection string do migrator, o único papel com BYPASSRLS, que enxerga todos os tenants.
    /// </remarks>
    public class FixedAdminVerifier {

        /// <summary>Nome lógico do catálogo nas mensagens e nos resultados.</summary>
        public const string CatalogName = "catalog";

        /// <summary>Tempo máximo de cada consulta (<c>statement_timeout</c>). Valor inicial, não medido.</summary>
        public const int StatementTimeoutSeconds = 60;

        private readonly SeedAdminFingerprint _fingerprint;

        public FixedAdminVerifier(SeedAdminFingerprint fingerprint) {
            _fingerprint = fingerprint;
        }

        /// <summary>
        /// Abre uma conexão SEM pool e já dentro de <c>BEGIN READ ONLY</c> (com <c>statement_timeout</c>). Quem chama
        /// descarta a conexão ao terminar, o que desfaz a transação. Depois de um erro a transação fica abortada: use uma
        /// conexão nova.
        /// </summary>
        public static async Task<NpgsqlConnection> OpenReadOnlyAsync(
            string connectionString, CancellationToken cancellationToken = default) {

            // Sem pool: a conexão vive só durante a verificação desta base (com N bases, um pool por base deixaria N
            // conexões ociosas no servidor).
            var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(connectionString) { Pooling = false }.ConnectionString);

            try {
                await connection.OpenAsync(cancellationToken);

                await using (var begin = new NpgsqlCommand("BEGIN READ ONLY", connection)) {
                    await begin.ExecuteNonQueryAsync(cancellationToken);
                }

                await using (var timeout = new NpgsqlCommand($"SET LOCAL statement_timeout = {StatementTimeoutSeconds * 1000}", connection)) {
                    await timeout.ExecuteNonQueryAsync(cancellationToken);
                }

                return connection;
            }
            catch {
                await connection.DisposeAsync();
                throw;
            }
        }

        /// <summary>Procura o administrador fixo na tabela de operadores de uma base de tenant.</summary>
        public async Task<DatabaseVerification> CheckTenantDatabaseAsync(
            string name, string migrationConnectionString, CancellationToken cancellationToken = default) {

            try {
                await using var connection = await OpenReadOnlyAsync(migrationConnectionString, cancellationToken);

                // Sem a tabela (base vazia ou ainda sem estrutura): "não contém".
                if (!await TableExistsAsync(connection, _fingerprint.Table, cancellationToken)) {
                    return new DatabaseVerification(name, DatabaseVerdict.Clean, null);
                }

                var sql = $"SELECT 1 FROM {Quote(_fingerprint.Table)} " +
                          $"WHERE {Quote(_fingerprint.IdColumn)} = @id OR lower({Quote(_fingerprint.EmailColumn)}) = lower(@email) LIMIT 1";

                return await ExistsAsync(connection, sql, cancellationToken)
                    ? new DatabaseVerification(name, DatabaseVerdict.Contaminated, "o identificador ou o e-mail fixo da migration SeedAdmin está presente")
                    : new DatabaseVerification(name, DatabaseVerdict.Clean, null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                return new DatabaseVerification(name, DatabaseVerdict.Unverifiable, Describe(exception));
            }
        }

        /// <summary>
        /// Varre o esquema <c>public</c> do catálogo atrás de colunas de texto com <c>email</c> no nome e de colunas
        /// <c>uuid</c> chamadas <c>operator_id</c>, e procura o e-mail fixo e o identificador fixo nelas. Os
        /// identificadores são sempre citados, nunca concatenados crus.
        /// </summary>
        public async Task<DatabaseVerification> CheckCatalogAsync(
            string catalogConnectionString, CancellationToken cancellationToken = default) {

            try {
                await using var connection = await OpenReadOnlyAsync(catalogConnectionString, cancellationToken);

                var candidates = new List<(string Table, string Column, bool IsUuid)>();
                await using (var list = new NpgsqlCommand(
                    """
                    select table_name, column_name, data_type
                      from information_schema.columns
                     where table_schema = 'public'
                       and ((data_type in ('text', 'character varying', 'character') and column_name ilike '%email%')
                         or (data_type = 'uuid' and column_name = 'operator_id'))
                     order by table_name, column_name
                    """, connection))
                await using (var reader = await list.ExecuteReaderAsync(cancellationToken)) {
                    while (await reader.ReadAsync(cancellationToken)) {
                        candidates.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2) == "uuid"));
                    }
                }

                foreach (var (table, column, isUuid) in candidates) {
                    var sql = isUuid
                        ? $"SELECT 1 FROM {Quote(table)} WHERE {Quote(column)} = @id LIMIT 1"
                        : $"SELECT 1 FROM {Quote(table)} WHERE lower({Quote(column)}) = lower(@email) LIMIT 1";

                    if (await ExistsAsync(connection, sql, cancellationToken)) {
                        return new DatabaseVerification(CatalogName, DatabaseVerdict.Contaminated,
                            $"dado do administrador fixo na tabela '{table}'");
                    }
                }

                return new DatabaseVerification(CatalogName, DatabaseVerdict.Clean, null);
            }
            catch (Exception exception) when (exception is not OperationCanceledException) {
                return new DatabaseVerification(CatalogName, DatabaseVerdict.Unverifiable, Describe(exception));
            }
        }

        private async Task<bool> ExistsAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken) {
            await using var command = new NpgsqlCommand(sql, connection);

            // Só os parâmetros que o SQL usa (a consulta de catálogo usa um ou o outro).
            if (sql.Contains("@id", StringComparison.Ordinal)) {
                command.Parameters.AddWithValue("id", _fingerprint.Id);
            }

            if (sql.Contains("@email", StringComparison.Ordinal)) {
                command.Parameters.AddWithValue("email", _fingerprint.Email);
            }

            return await command.ExecuteScalarAsync(cancellationToken) is not null;
        }

        private static async Task<bool> TableExistsAsync(NpgsqlConnection connection, string table, CancellationToken cancellationToken) {
            await using var command = new NpgsqlCommand("SELECT to_regclass(@name) IS NOT NULL", connection);
            command.Parameters.AddWithValue("name", Quote(table));
            return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
        }

        // Identificador citado: aspas duplas, com aspas internas dobradas.
        private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";

        // Motivo curto da falha: só o tipo e, no caso do banco, o SQLSTATE. Nunca a mensagem (poderia trazer servidor,
        // porta, usuário ou trecho de dado).
        private static string Describe(Exception exception) => exception switch {
            PostgresException postgres => $"erro do banco (SQLSTATE {postgres.SqlState})",
            NpgsqlException or SocketException or TimeoutException => $"base inacessível ({exception.GetType().Name})",
            _ => $"falha inesperada ({exception.GetType().Name})"
        };
    }
}
