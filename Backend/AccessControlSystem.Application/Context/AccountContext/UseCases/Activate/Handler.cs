using AccessControlSystem.Application.Context.AccountContext.UseCases.Activate.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Activate {
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

            #region 02 - Busca a conta a ser reativada
            var @operator = await _repository.GetUserById(request.Id, cancellationToken);

            if (@operator == null) {
                return new Response("Conta não encontrada", 404);
            }
            #endregion

            #region 03 - Reativa e persiste
            @operator.Activate();
            await _repository.ActivateAsync(@operator, cancellationToken);
            #endregion

            #region 04 - Retorna a resposta
            return new Response(
                "Operador reativado com sucesso", 200,
                new ResponseData(@operator.Id, @operator.Email, @operator.Role, @operator.IsActive));
            #endregion
        }
    }
}
