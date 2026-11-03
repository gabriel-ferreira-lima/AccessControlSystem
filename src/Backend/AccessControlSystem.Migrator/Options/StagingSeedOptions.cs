using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Migrator.Options {

    /// <summary>
    /// Senhas dos operadores fictícios do <c>seed-staging</c> (seção <c>StagingSeed</c>), sempre de fora do
    /// repositório: variável de ambiente <c>StagingSeed__AdminPassword</c> etc. (na spec 05b virão do Secrets
    /// Manager, sem mudar o código). Nenhuma senha fica fixa no código nem em arquivo. Cada uma passa pelas regras
    /// de senha do sistema antes de qualquer escrita.
    /// </summary>
    public class StagingSeedOptions {

        public const string SectionName = "StagingSeed";

        [Required(AllowEmptyStrings = false, ErrorMessage = "StagingSeed:AdminPassword é obrigatória.")]
        public string AdminPassword { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "StagingSeed:OperatorPasswordTenantA é obrigatória.")]
        public string OperatorPasswordTenantA { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false, ErrorMessage = "StagingSeed:OperatorPasswordTenantB é obrigatória.")]
        public string OperatorPasswordTenantB { get; set; } = string.Empty;
    }
}
