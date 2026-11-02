using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Api.Options {

    /// <summary>
    /// Segredos da Api (seção <c>Secrets</c>). Vêm de variáveis de ambiente, user-secrets ou do
    /// <c>.env</c> do compose, nunca de <c>appsettings.json</c> versionado. A chave é obrigatória e o tamanho
    /// mínimo (32 bytes, 256 bits) é conferido por <see cref="SecretsOptionsValidator"/>.
    /// </summary>
    public class SecretsOptions {

        public const string SectionName = "Secrets";

        [Required(AllowEmptyStrings = false, ErrorMessage = "Secrets:JwtPrivateKey é obrigatória.")]
        public string JwtPrivateKey { get; set; } = string.Empty;
    }
}
