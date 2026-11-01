using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Migrator.Options {

    /// <summary>
    /// Senhas dos operadores sintéticos do <c>seed-dev</c> (seção <c>DevSeed</c>), preenchidas pelo
    /// <c>.env</c> do compose. Nenhuma senha fica fixa no código. O e-mail do administrador de
    /// desenvolvimento é fixo (<c>admin@admin.com.br</c>, FR-030).
    /// </summary>
    public class DevSeedOptions {

        public const string SectionName = "DevSeed";

        [Required(AllowEmptyStrings = false, ErrorMessage = "DevSeed:AdminPassword é obrigatória.")]
        public string AdminPassword { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "DevSeed:OperatorPasswordTenantA é obrigatória.")]
        public string OperatorPasswordTenantA { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "DevSeed:OperatorPasswordTenantB é obrigatória.")]
        public string OperatorPasswordTenantB { get; set; } = string.Empty;
    }
}
