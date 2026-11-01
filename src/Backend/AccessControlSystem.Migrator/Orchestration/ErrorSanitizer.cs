using System.Text.RegularExpressions;
using Npgsql;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>
    /// Tira connection string e senha de qualquer texto que vá para o catálogo, para a saída ou para o
    /// log: o motivo de uma falha nunca pode vazar segredo (FR-023).
    /// </summary>
    public static partial class ErrorSanitizer {

        public const int MaxLength = 1000;
        public const string OmittedConnectionString = "[connection string omitida]";

        public static string FromException(Exception exception, params string?[] connectionStrings) =>
            Sanitize($"{exception.GetType().Name}: {exception.Message}", connectionStrings);

        public static string Sanitize(string message, params string?[] connectionStrings) {
            // Das mais longas para as mais curtas: uma connection string pode conter outra.
            foreach (var connectionString in connectionStrings
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .OrderByDescending(x => x!.Length)) {

                message = message.Replace(connectionString!, OmittedConnectionString, StringComparison.Ordinal);

                try {
                    var password = new NpgsqlConnectionStringBuilder(connectionString).Password;
                    if (!string.IsNullOrEmpty(password)) {
                        message = message.Replace(password, "***", StringComparison.Ordinal);
                    }
                }
                catch (ArgumentException) {
                    // connection string malformada: já foi removida inteira acima
                }
            }

            // Rede de segurança para um pedaço de connection string que não veio de configuração conhecida.
            message = PasswordPair().Replace(message, "${key}=***");

            return message.Length <= MaxLength ? message : message[..MaxLength];
        }

        [GeneratedRegex(@"(?<key>password|pwd)\s*=\s*[^;\s]+", RegexOptions.IgnoreCase)]
        private static partial Regex PasswordPair();
    }
}
