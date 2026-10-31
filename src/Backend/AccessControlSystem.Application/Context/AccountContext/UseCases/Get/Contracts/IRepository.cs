using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Enums;
using AccessControlSystem.Application.SharedContext.UseCases;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Contracts {
    public interface IRepository {
        Task<PagedResponse<OperatorItem>> GetOperatorList(
            int page, int size, EActiveFilter isActive, CancellationToken cancellationToken = default);
    }
}