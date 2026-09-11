using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Update {
    public record Request(
        Guid Id,
        string? Email = null,
        string? Password = null,
        ERole? Role = null) : IRequest {
    }
}
