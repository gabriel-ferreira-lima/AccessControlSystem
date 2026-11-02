namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts {

    /// <summary>
    /// Resposta que informa quanto tempo o cliente deve esperar (cabeçalho <c>Retry-After</c>, escrito pela Api; não vai
    /// no corpo). Só o <c>Authenticate</c> a usa, então mora aqui; vira <c>SharedContext</c> quando um segundo use case precisar.
    /// </summary>
    public interface IRetryAfterResponse {

        TimeSpan? RetryAfter { get; }
    }
}
