namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts {

    /// <summary>
    /// Limite de tentativas de login por CONTA (tenant + e-mail normalizado), valendo para todas as origens somadas
    /// (FR-017, FR-018). A admissão é atômica e anterior à verificação da senha: sem janela para uma rajada simultânea
    /// passar pela consulta antes de qualquer tentativa ser contada. A implementação atual é em memória da instância;
    /// a spec 06 implementa o MESMO contrato no Redis (com <c>INCR</c> atômico), sem mudar a regra.
    /// </summary>
    public interface ILoginThrottle {

        /// <summary>
        /// Admissão ATÔMICA de uma tentativa de login para a conta. Se a conta está bloqueada, devolve o tempo restante e
        /// não conta nada; senão conta a tentativa como falha provisória e, se ela atingir o máximo, bloqueia pela espera
        /// do nível. Devolve <c>null</c> quando a tentativa pode seguir (inclusive a que acabou de atingir o máximo).
        /// Falha fechada: se não puder rastrear a conta (registro saturado), devolve um bloqueio, nunca <c>null</c> por engano.
        /// </summary>
        Task<TimeSpan?> TryAcquireAttemptAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);

        /// <summary>Login com sucesso: zera contagem, nível e bloqueio da conta.</summary>
        Task ResetAsync(Guid tenantId, string email, CancellationToken cancellationToken = default);
    }
}
