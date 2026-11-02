using Microsoft.Extensions.Options;

namespace AccessControlSystem.Api.Options {

    /// <summary>
    /// Falha a partida quando a chave de assinatura é curta demais (FR-012). A ausência da chave já é recusada pelo
    /// <c>[Required]</c> de <see cref="SecretsOptions"/>; aqui só se confere o tamanho. A mensagem diz qual
    /// configuração está errada e NUNCA mostra o valor da chave.
    /// </summary>
    public class SecretsOptionsValidator : IValidateOptions<SecretsOptions> {

        public ValidateOptionsResult Validate(string? name, SecretsOptions options) {
            if (string.IsNullOrWhiteSpace(options.JwtPrivateKey)) {
                return ValidateOptionsResult.Success;
            }

            if (JwtSigningKey.GetByteCount(options.JwtPrivateKey) < JwtSigningKey.MinimumBytes) {
                return ValidateOptionsResult.Fail(
                    $"{SecretsOptions.SectionName}:{nameof(SecretsOptions.JwtPrivateKey)} deve ter pelo menos {JwtSigningKey.MinimumBytes} bytes.");
            }

            return ValidateOptionsResult.Success;
        }
    }
}
