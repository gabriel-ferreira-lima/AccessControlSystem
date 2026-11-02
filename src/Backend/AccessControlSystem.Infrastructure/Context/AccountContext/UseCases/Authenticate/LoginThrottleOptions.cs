using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate {

    /// <summary>
    /// Limite de login por conta (seção <c>LoginProtection:Account</c>). Os padrões são os da spec: 5 tentativas por nível,
    /// espera de 1 min que dobra até 15 min, registro descartado depois de 15 min sem tentativa nem bloqueio. As faixas
    /// (piso e teto) e as regras entre campos são validadas na partida, para um valor errado não desligar a proteção.
    /// </summary>
    public class LoginThrottleOptions : IValidatableObject {

        public const string SectionName = "LoginProtection:Account";

        /// <summary>Tentativas contadas por nível até bloquear a conta.</summary>
        [Range(3, 100, ErrorMessage = "LoginProtection:Account:MaxFailures deve estar entre 3 e 100.")]
        public int MaxFailures { get; set; } = 5;

        /// <summary>Primeira espera, em segundos (dobra a cada novo bloqueio).</summary>
        [Range(1, 3600, ErrorMessage = "LoginProtection:Account:BaseWaitSeconds deve estar entre 1 e 3600.")]
        public int BaseWaitSeconds { get; set; } = 60;

        /// <summary>Teto da espera, em segundos.</summary>
        [Range(1, 86400, ErrorMessage = "LoginProtection:Account:MaxWaitSeconds deve estar entre 1 e 86400.")]
        public int MaxWaitSeconds { get; set; } = 900;

        /// <summary>Descarte do registro sem tentativa nem bloqueio, em segundos (FR-032).</summary>
        [Range(1, 86400, ErrorMessage = "LoginProtection:Account:IdleExpirySeconds deve estar entre 1 e 86400.")]
        public int IdleExpirySeconds { get; set; } = 900;

        /// <summary>Tamanho máximo do registro, por instância.</summary>
        [Range(100, 10_000_000, ErrorMessage = "LoginProtection:Account:MaxTrackedAccounts deve estar entre 100 e 10000000.")]
        public int MaxTrackedAccounts { get; set; } = 100_000;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext) {
            if (MaxWaitSeconds < BaseWaitSeconds) {
                yield return new ValidationResult(
                    "LoginProtection:Account:MaxWaitSeconds deve ser maior ou igual a BaseWaitSeconds.",
                    [nameof(MaxWaitSeconds)]);
            }

            // O registro nunca pode ser descartado no meio de um bloqueio.
            if (IdleExpirySeconds < MaxWaitSeconds) {
                yield return new ValidationResult(
                    "LoginProtection:Account:IdleExpirySeconds deve ser maior ou igual a MaxWaitSeconds.",
                    [nameof(IdleExpirySeconds)]);
            }
        }
    }
}
