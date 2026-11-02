using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.SharedContext.UseCases.Validation {
    public static class ContractExtensions {

        /// <summary>
        /// Notifica quando o texto passa de <paramref name="maxLength"/> caracteres. Conta caracteres Unicode (pontos de
        /// código, como o varchar do PostgreSQL), não bytes nem unidades UTF-16; o limite é inclusivo e <c>null</c> está
        /// dentro dele. A <paramref name="message"/> não deve conter o valor recebido.
        /// </summary>
        public static Contract<Notification> IsMaxLength(
            this Contract<Notification> contract, string? value, int maxLength, string property, string message) {
            if (!IsWithinLimit(value, maxLength)) {
                contract.AddNotification(property, message);
            }

            return contract;
        }

        private static bool IsWithinLimit(string? value, int maxLength) {
            if (value is null) {
                return true;
            }

            // Curto-circuito: cada ponto de código ocupa no máximo 2 unidades UTF-16, então só no meio é preciso contar
            // (uma senha de 1 MB é recusada sem varrer o texto).
            if (value.Length <= maxLength) {
                return true;
            }

            if (value.Length > maxLength * 2) {
                return false;
            }

            var count = 0;
            foreach (var _ in value.EnumerateRunes()) {
                if (++count > maxLength) {
                    return false;
                }
            }

            return true;
        }
    }
}
