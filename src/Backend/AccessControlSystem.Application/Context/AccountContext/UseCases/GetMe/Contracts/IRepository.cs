using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.GetMe.Contracts {
    public interface IRepository {

        Task<Operator?> GetUserById(Guid id, CancellationToken cancellationToken = default);
    }
}
