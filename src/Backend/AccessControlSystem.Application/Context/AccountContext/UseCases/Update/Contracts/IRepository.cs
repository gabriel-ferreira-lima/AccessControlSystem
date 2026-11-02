using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Update.Contracts {
    public interface IRepository {

        Task<Operator> UpdateAsync(Operator @operator, CancellationToken cancellationToken = default);

        Task<Operator?> GetOperatorByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<bool> EmailExistsAsync(string email, Guid exceptId, CancellationToken cancellationToken = default);
    }
}
