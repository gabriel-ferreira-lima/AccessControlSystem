using AccessControlSystem.Application.Context.AccountContext.UseCases.Get.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using System.Net;


namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Get {
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

            #region 02 - Busca a página
            var result = await _repository.GetOperatorList(request.Page, request.Size, request.IsActive, cancellationToken);
            #endregion

            var data = new ResponseData(
                result.Items, result.Page, result.Size, result.Total, result.TotalPages);

            return new Response("Lista retornada com sucesso", 200, data);
        }
    }
}
