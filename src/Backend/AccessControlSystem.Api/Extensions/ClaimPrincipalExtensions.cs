using System.Globalization;
using System.Security.Claims;

namespace AccessControlSystem.Api.Extensions {
    public static class ClaimPrincipalExtensions {

        public static string Id(this ClaimsPrincipal user)
            => user.Claims.FirstOrDefault(c => c.Type == "Id")?.Value ?? string.Empty;

        public static string TenantId(this ClaimsPrincipal user)
            => user.Claims.FirstOrDefault(c => c.Type == "tenant")?.Value ?? string.Empty;

        public static string Role(this ClaimsPrincipal user)
            => user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? string.Empty;

        /// <summary>
        /// Versão de segurança do operador gravada no token. <c>null</c> quando a claim não existe ou não é um
        /// inteiro não negativo (só dígitos): o token é recusado antes de qualquer consulta ao banco.
        /// </summary>
        public static int? SecurityVersion(this ClaimsPrincipal user) {
            var value = user.Claims.FirstOrDefault(c => c.Type == "security_version")?.Value;

            return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var version)
                ? version
                : null;
        }
    }
}
