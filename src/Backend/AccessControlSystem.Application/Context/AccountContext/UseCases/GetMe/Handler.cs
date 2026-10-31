using AccessControlSystem.Application.Context.AccountContext.UseCases.GetMe.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.GetMe {
    public class Handler : IHandler<Request, Response> {

        private readonly IRepository _repository;
        public Handler(IRepository repository) {
            _repository = repository;
        }
        public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken = default) {
            #region 01 - Valida se a request esta correta 
            var res = Specification.Ensure(request);

            if (!res.IsValid) {
                return new Response("Requisição inválida", 400, res.Notifications);
            }
            #endregion

            #region 02 - Busca informações do usuário
            var @operator = await _repository.GetUserById(request.Id, cancellationToken);

            if (@operator == null) {
                return new Response("Sessão invalida - faça o login novamente", 401);
            }
            #endregion

            #region 03 - Retorna informações do usuário
            return new Response("Operador retornado com sucesso", 200, new ResponseData(@operator.Id, @operator.Email, @operator.Role));
            #endregion
        }
    }
}
