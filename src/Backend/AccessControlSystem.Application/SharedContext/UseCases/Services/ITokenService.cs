using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.SharedContext.UseCases.Services {
    public interface ITokenService {

        string Generate(Operator @operator);
    }
}
