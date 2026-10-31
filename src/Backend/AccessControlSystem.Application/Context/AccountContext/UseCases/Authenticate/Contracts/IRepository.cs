using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts {
    public interface IRepository {

        Task<Operator?> GetUserByEmailAsync(string email, CancellationToken cancellationToken = default);
    }
}
