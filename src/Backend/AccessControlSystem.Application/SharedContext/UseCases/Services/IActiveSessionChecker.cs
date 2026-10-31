namespace AccessControlSystem.Application.SharedContext.UseCases.Services {

    public interface IActiveSessionChecker {
        Task<bool> IsActiveAsync(Guid operatorId, CancellationToken cancellationToken = default);
    }
}
