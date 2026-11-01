namespace AccessControlSystem.Infrastructure.Catalog.Models {

    /// <summary>Situação da atualização da estrutura de uma base de dados (registro no catálogo).</summary>
    public enum EMigrationStatus {

        /// <summary>Cadastrada, ainda não atualizada.</summary>
        Pending = 1,

        /// <summary>Em atualização. Se aparecer numa nova execução, é resto de uma queda e é reprocessada.</summary>
        Running = 2,

        /// <summary><c>schema_version</c> é a última migration aplicada.</summary>
        Succeeded = 3,

        /// <summary>A última tentativa falhou; <c>migration_error</c> tem o motivo.</summary>
        Failed = 4
    }
}
