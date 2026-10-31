using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Get {
    public static class Specification {

        public static Contract<Notification> Ensure(Request request) {
            return new Contract<Notification>()
                .Requires()
                .IsGreaterThan(request.Page, 0, "Page", "A página deve ser 1 ou maior")
                .IsGreaterThan(request.Size, 0, "Size", "O tamanho da página deve ser 1 ou maior")
                .IsLowerThan(request.Size, 101, "Size", "O tamanho da página não pode passar de 100")
                .IsTrue(Enum.IsDefined(request.IsActive), "IsActive", "Valor inválido para IsActive (use 0, 1 ou 2)");
        }
    }
}
