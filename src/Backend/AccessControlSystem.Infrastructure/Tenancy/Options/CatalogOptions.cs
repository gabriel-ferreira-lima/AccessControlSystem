using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Infrastructure.Tenancy.Options {

    /// <summary>
    /// Connection string do catálogo (<c>ConnectionStrings:Catalog</c>), no papel da aplicação
    /// (somente leitura). Obrigatória: a Api não inicia sem ela.
    /// </summary>
    public class CatalogOptions {

        public const string SectionName = "ConnectionStrings";

        [Required(AllowEmptyStrings = false, ErrorMessage = "ConnectionStrings:Catalog é obrigatória.")]
        public string Catalog { get; set; } = string.Empty;
    }
}
