using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Activate.Contracts {
    public interface IRepository {

        Task<Operator?> GetOperatorByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task ActivateAsync(Operator @operator, CancellationToken cancellationToken = default);
    }
}
