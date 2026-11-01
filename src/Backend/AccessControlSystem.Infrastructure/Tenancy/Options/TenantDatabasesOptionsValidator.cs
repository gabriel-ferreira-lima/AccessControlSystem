using Microsoft.Extensions.Options;

namespace AccessControlSystem.Infrastructure.Tenancy.Options {

    /// <summary>
    /// Toda base configurada em <c>TenantDatabases</c> precisa de <c>ConnectionString</c> (a da Api).
    /// A mensagem cita o nome da base, nunca o valor da connection string.
    /// </summary>
    public class TenantDatabasesOptionsValidator : IValidateOptions<TenantDatabasesOptions> {

        public ValidateOptionsResult Validate(string? name, TenantDatabasesOptions options) {
            var failures = options
                .Where(x => string.IsNullOrWhiteSpace(x.Value.ConnectionString))
                .Select(x => $"TenantDatabases:{x.Key}:ConnectionString é obrigatória.")
                .ToList();

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
