using AccessControlSystem.Application.Context.AccountContext.UseCases.Deactivate.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Deactivate {
    public class Handler : IHandler<Request, Response> {

        private readonly IRepository _repository;

        public Handler(IRepository repository) {
            _repository = repository;
        }

        public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken = default) {

            #region 01 - Valida a requisição
            var res = Specification.Ensure(request);

            if (!res.IsValid) {
                return new Response("Requisição inválida", 400, res.Notifications);
            }
            #endregion

            #region 02 - Busca a conta a ser desativada
            var @operator = await _repository.GetUserById(request.Id, cancellationToken);

            if (@operator == null) {
                return new Response("Conta não encontrada", 404);
            }
            #endregion

            #region 03 - Não deixa o sistema sem nenhum admin ativo
            if (@operator.Role == ERole.Admin && await _repository.IsLastActiveAdminAsync(request.Id, cancellationToken)) {
                return new Response("Não é possível desativar o último administrador ativo", 400);
            }
            #endregion

            #region 04 - Desativa e persiste
            @operator.Deactivate();
            await _repository.DeactivateAsync(@operator, cancellationToken);
            #endregion

            #region 05 - Retorna a resposta
            return new Response(
                "Operador desativado com sucesso", 200,
                new ResponseData(@operator.Id, @operator.Email, @operator.Role, @operator.IsActive));
            #endregion
        }
    }
}
