using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate {
    public class Handler : IHandler<Request, Response> {

        private readonly IRepository _repository;
        private readonly ITokenService _tokenService;

        public Handler(IRepository repository, ITokenService tokenService) {
            _repository = repository;
            _tokenService = tokenService;
        }
        public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken = default) {

            #region 01 - Valida a se a requisição esta correta
            var res = Specification.Ensure(request);
            if (!res.IsValid) {
                return new Response("Requisição inválida", 400, res.Notifications);
            }

            #endregion

            #region 02 - Valida se o usuário existe e se a senha esta correta
            Operator? @operator;

            @operator = await _repository.GetUserByEmailAsync(request.Email, cancellationToken);

            if (@operator == null || !@operator.IsActive || !@operator.Password.Verify(request.Password)) {
                return new Response("Credenciais inválidas", 401);
            }
            #endregion

            var token = _tokenService.Generate(@operator);

            #region 03 - Retorna o usuário autenticado
            var data = new ResponseData {
                Token = token,
                Id = @operator.Id,
                Email = @operator.Email,
                Role = @operator.Role
            };

            return new Response("Usuário autenticado com sucesso", 200, data);
            #endregion

        }
    }
}
