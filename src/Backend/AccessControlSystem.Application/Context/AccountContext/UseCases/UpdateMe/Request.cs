using AccessControlSystem.Application.SharedContext.UseCases.Contracts;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe {
    public record Request(
        Guid Id,
        string? Email = null,
        string? Password = null) : IRequest;
}
