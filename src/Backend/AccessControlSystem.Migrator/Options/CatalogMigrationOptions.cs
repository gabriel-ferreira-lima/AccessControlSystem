using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Migrator.Options {

    /// <summary>
    /// Connection string do catálogo com o usuário migrator (<c>ConnectionStrings:CatalogMigration</c>):
    /// DDL e escrita do estado de atualização. Obrigatória.
    /// </summary>
    public class CatalogMigrationOptions {

        public const string SectionName = "ConnectionStrings";

        [Required(AllowEmptyStrings = false, ErrorMessage = "ConnectionStrings:CatalogMigration é obrigatória.")]
        public string CatalogMigration { get; set; } = string.Empty;
    }
}
