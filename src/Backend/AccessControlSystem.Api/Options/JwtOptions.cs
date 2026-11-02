using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Api.Options {

    /// <summary>
    /// Identidade do token (seção <c>Jwt</c>): quem emite (<c>Issuer</c>) e para quem (<c>Audience</c>). A Api só
    /// aceita um token com os dois valores iguais a estes. Não são segredos, mas são obrigatórios e ficam vazios
    /// no <c>appsettings.json</c> versionado de propósito: a Api não sobe por acidente com um padrão. Em cada
    /// ambiente os valores DEVEM ser diferentes, para um token de um ambiente nunca valer em outro.
    /// </summary>
    public class JwtOptions {

        public const string SectionName = "Jwt";

        [Required(AllowEmptyStrings = false, ErrorMessage = "Jwt:Issuer é obrigatório.")]
        public string Issuer { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "Jwt:Audience é obrigatório.")]
        public string Audience { get; set; } = string.Empty;
    }
}
