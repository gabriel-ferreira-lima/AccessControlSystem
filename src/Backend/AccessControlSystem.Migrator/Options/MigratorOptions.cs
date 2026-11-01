using System.ComponentModel.DataAnnotations;

namespace AccessControlSystem.Migrator.Options {

    /// <summary>
    /// Ajustes do passo <c>migrate</c> (seção <c>Migrator</c>). Os padrões são valores iniciais
    /// <b>não medidos</b>: servem para começar e devem ser calibrados com a medição da spec 06.
    /// Todos precisam ser maiores que zero (validado na partida).
    /// </summary>
    public class MigratorOptions {

        public const string SectionName = "Migrator";

        /// <summary>Quantas bases são lidas do catálogo por lote (paginação por chave).</summary>
        [Range(1, 100_000, ErrorMessage = "Migrator:BatchSize deve ser maior que zero.")]
        public int BatchSize { get; set; } = 10;

        /// <summary>Quantas bases de um lote são atualizadas ao mesmo tempo.</summary>
        [Range(1, 1_000, ErrorMessage = "Migrator:MaxDegreeOfParallelism deve ser maior que zero.")]
        public int MaxDegreeOfParallelism { get; set; } = 2;

        /// <summary>Quanto esperar pela trava global antes de desistir (sai com código 4).</summary>
        [Range(1, 86_400, ErrorMessage = "Migrator:LockTimeoutSeconds deve ser maior que zero.")]
        public int LockTimeoutSeconds { get; set; } = 600;
    }
}
