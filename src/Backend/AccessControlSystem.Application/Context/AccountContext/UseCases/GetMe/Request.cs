using AccessControlSystem.Application.SharedContext.UseCases.Contracts;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.GetMe {
    public record Request(Guid Id) : IRequest {
    }
}
