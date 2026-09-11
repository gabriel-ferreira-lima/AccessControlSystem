using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Create {
    public record Request(string Email, string Password, ERole Role) : IRequest {
    }
}
