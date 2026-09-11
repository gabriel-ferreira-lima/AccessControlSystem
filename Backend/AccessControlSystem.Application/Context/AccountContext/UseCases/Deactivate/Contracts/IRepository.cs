using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Deactivate.Contracts {
    public interface IRepository {

        Task<Operator?> GetUserById(Guid id, CancellationToken cancellationToken = default);

        Task DeactivateAsync(Operator @operator, CancellationToken cancellationToken = default);

        Task<bool> IsLastActiveAdminAsync(Guid exceptId, CancellationToken cancellationToken = default);
    }
}
