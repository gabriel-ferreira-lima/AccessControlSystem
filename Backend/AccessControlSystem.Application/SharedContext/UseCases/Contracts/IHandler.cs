namespace AccessControlSystem.Application.SharedContext.UseCases.Contracts {
    public interface IHandler<TRequest, TResponse> 
        where TRequest : IRequest 
        where TResponse : IResponse {


        Task<TResponse> HandleAsync(TRequest request, CancellationToken cancellationToken = default);
    }


}
