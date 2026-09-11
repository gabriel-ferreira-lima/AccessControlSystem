using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Enums;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Get {
    public record Request (int Size = 10, int Page = 1, EActiveFilter IsActive = EActiveFilter.All) : IRequest {
    }
}
