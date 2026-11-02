using AccessControlSystem.Api.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace AccessControlSystem.Migrator.Orchestration {

    /// <summary>
    /// A identidade fixa do administrador que a migration <c>SeedAdmin</c> cria (tabela, colunas, id e e-mail), lida
    /// DA PRÓPRIA migration (<c>new SeedAdmin().UpOperations</c>), sem duplicar nenhum valor à mão: se a migration
    /// mudar, a verificação muda junto. Se ela mudar de FORMA e a extração não achar o que espera, a leitura FALHA
    /// ALTO (<see cref="InvalidOperationException"/>): nunca devolve um valor vazio, que faria a verificação aprovar tudo.
    /// </summary>
    /// <remarks>O hash da senha não é usado na busca. Qualquer dado fixo basta para reprovar: o id OU o e-mail.</remarks>
    public sealed record SeedAdminFingerprint(string Table, string IdColumn, string EmailColumn, Guid Id, string Email) {

        // Nomes das COLUNAS (a estrutura que a extração procura); os VALORES vêm sempre da migration.
        private const string IdColumnName = "id";
        private const string EmailColumnName = "email";

        /// <summary>A identidade fixa da migration <c>SeedAdmin</c> do próprio sistema.</summary>
        public static SeedAdminFingerprint Current => From(new SeedAdmin());

        /// <summary>Lê a identidade da (única) <c>InsertData</c> da migration. Falha alto se a forma não for a esperada.</summary>
        public static SeedAdminFingerprint From(Migration migration) {
            var name = migration.GetType().Name;
            var inserts = migration.UpOperations.OfType<InsertDataOperation>().ToList();

            if (inserts.Count != 1) {
                throw new InvalidOperationException(
                    $"A migration {name} deveria ter exatamente uma InsertData, mas tem {inserts.Count}: o administrador fixo não pôde ser identificado.");
            }

            var insert = inserts[0];
            var idIndex = Array.IndexOf(insert.Columns, IdColumnName);
            var emailIndex = Array.IndexOf(insert.Columns, EmailColumnName);

            if (idIndex < 0) {
                throw new InvalidOperationException($"A InsertData da migration {name} não tem a coluna '{IdColumnName}'.");
            }

            if (emailIndex < 0) {
                throw new InvalidOperationException($"A InsertData da migration {name} não tem a coluna '{EmailColumnName}'.");
            }

            if (insert.Values.GetLength(0) != 1) {
                throw new InvalidOperationException(
                    $"A InsertData da migration {name} insere {insert.Values.GetLength(0)} linhas; era esperada exatamente uma.");
            }

            var id = insert.Values[0, idIndex] switch {
                Guid guid => guid,
                string text when Guid.TryParse(text, out var parsed) => parsed,
                _ => throw new InvalidOperationException($"O valor da coluna '{IdColumnName}' na migration {name} não é um identificador (Guid).")
            };

            if (id == Guid.Empty) {
                throw new InvalidOperationException($"O valor da coluna '{IdColumnName}' na migration {name} é um identificador vazio.");
            }

            if (insert.Values[0, emailIndex] is not string email || string.IsNullOrWhiteSpace(email)) {
                throw new InvalidOperationException($"O valor da coluna '{EmailColumnName}' na migration {name} está vazio.");
            }

            return new SeedAdminFingerprint(insert.Table, IdColumnName, EmailColumnName, id, email);
        }

        /// <summary>O e-mail é comparado sem diferenciar maiúsculas de minúsculas.</summary>
        public bool MatchesEmail(string? candidate) =>
            string.Equals(candidate, Email, StringComparison.OrdinalIgnoreCase);
    }
}
