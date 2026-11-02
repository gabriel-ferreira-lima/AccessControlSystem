using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Exceptions;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate {
    public class Handler : IHandler<Request, Response> {

        private readonly IRepository _repository;
        private readonly ITokenService _tokenService;
        private readonly IPasswordVerifier _passwordVerifier;

        public Handler(IRepository repository, ITokenService tokenService, IPasswordVerifier passwordVerifier) {
            _repository = repository;
            _tokenService = tokenService;
            _passwordVerifier = passwordVerifier;
        }
        public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken = default) {
            try {

                #region 01 - Valida se a requisição esta correta
                var res = Specification.Ensure(request);
                if (!res.IsValid) {
                    return new Response("Requisição inválida", 400, res.Notifications);
                }
                #endregion

                #region 02 - Normaliza o e-mail (sem espaços nas pontas e em minúsculas)
                var email = new Email(request.Email);
                #endregion

                #region 03 - Busca o operador
                var @operator = await _repository.GetOperatorByEmailAsync(email.Address, cancellationToken);
                #endregion

                #region 04 - Verifica a senha (SEMPRE, existindo o operador ou não)
                // O mesmo trabalho nos três casos de falha (e-mail inexistente, conta inativa, senha errada) para o tempo de
                // resposta não revelar quais e-mails estão cadastrados nem quais contas estão inativas.
                var passwordMatches = _passwordVerifier.Verify(@operator, request.Password ?? string.Empty);
                #endregion

                #region 05 - Decide (a mesma resposta nos três casos de falha)
                if (@operator is null || !@operator.IsActive || !passwordMatches) {
                    return new Response("Credenciais inválidas", 401);
                }
                #endregion

                var token = _tokenService.Generate(@operator);

                #region 06 - Retorna o usuário autenticado
                var data = new ResponseData {
                    Token = token,
                    Id = @operator.Id,
                    Email = @operator.Email,
                    Role = @operator.Role
                };

                return new Response("Usuário autenticado com sucesso", 200, data);
                #endregion

            }
            catch (DomainException domainException) {
                return new Response(domainException.Message, 400);
            }
        }
    }
}
