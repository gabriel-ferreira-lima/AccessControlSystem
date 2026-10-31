using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Update.Contracts {
    public interface IRepository {

        Task<Operator> UpdateAsync(Operator @operator, CancellationToken cancellationToken = default);

        Task<Operator?> GetUserById(Guid id, CancellationToken cancellationToken = default);

        Task<bool> CheckIfEmailIsNotUsed(string email, Guid id, CancellationToken cancellationToken = default);
    }
}
