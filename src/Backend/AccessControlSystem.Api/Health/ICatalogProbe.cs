namespace AccessControlSystem.Api.Health {

    /// <summary>
    /// Sonda do catálogo usada pela verificação "pronta" (<c>/health/ready</c>). É a costura de teste: os testes a
    /// trocam por uma sonda que conta chamadas, trava ou falha.
    /// </summary>
    public interface ICatalogProbe {

        /// <summary>Completa quando o catálogo responde; <b>lança</b> quando não (inclusive por cancelamento).</summary>
        Task ProbeAsync(CancellationToken cancellationToken);
    }
}
