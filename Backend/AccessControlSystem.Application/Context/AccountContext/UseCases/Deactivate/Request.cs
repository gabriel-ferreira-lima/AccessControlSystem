using AccessControlSystem.Application.SharedContext.UseCases.Contracts;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Deactivate {
    public record Request(Guid Id, Guid RequestedBy) : IRequest;
}
