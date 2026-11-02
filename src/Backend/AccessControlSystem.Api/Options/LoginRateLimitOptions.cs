using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Api.Options {

    /// <summary>
    /// Limite de tentativas de login por origem (seção <c>LoginProtection:Ip</c>). Configurável, mas com piso e teto
    /// validados na partida, para um valor errado não desligar a proteção. Os padrões são os de sempre: 20 por minuto.
    /// </summary>
    public class LoginRateLimitOptions {

        public const string SectionName = "LoginProtection:Ip";

        [Range(1, 1000, ErrorMessage = "LoginProtection:Ip:PermitLimit deve estar entre 1 e 1000.")]
        public int PermitLimit { get; set; } = 20;

        [Range(1, 3600, ErrorMessage = "LoginProtection:Ip:WindowSeconds deve estar entre 1 e 3600.")]
        public int WindowSeconds { get; set; } = 60;
    }
}
