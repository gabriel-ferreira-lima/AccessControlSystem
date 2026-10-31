using AccessControlSystem.Application.SharedContext.UseCases.Contracts;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Activate {
    public record Request(Guid Id) : IRequest;
}
