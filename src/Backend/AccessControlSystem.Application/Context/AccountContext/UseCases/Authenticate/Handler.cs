using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Application.SharedContext.Tenancy;
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
        private readonly ILoginThrottle _loginThrottle;
        private readonly ITenantContext _tenantContext;

        public Handler(
            IRepository repository,
            ITokenService tokenService,
            IPasswordVerifier passwordVerifier,
            ILoginThrottle loginThrottle,
            ITenantContext tenantContext) {
            _repository = repository;
            _tokenService = tokenService;
            _passwordVerifier = passwordVerifier;
            _loginThrottle = loginThrottle;
            _tenantContext = tenantContext;
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

                #region 03 - Admite a tentativa (atômico, ANTES do hash)
                // A tentativa é contada como falha provisória no mesmo instante em que é admitida: só 5 por nível chegam à
                // verificação de senha, por mais requisições simultâneas que cheguem. Conta bloqueada: 429 sem tocar no
                // banco nem calcular hash. A conta vale para qualquer origem e existindo o e-mail ou não (FR-018, FR-020).
                var retryAfter = await _loginThrottle.TryAcquireAttemptAsync(_tenantContext.TenantId, email.Address, cancellationToken);
                if (retryAfter is { } wait) {
                    return Response.TooManyAttempts(wait);
                }
                #endregion

                #region 04 - Busca o operador
                var @operator = await _repository.GetOperatorByEmailAsync(email.Address, cancellationToken);
                #endregion

                #region 05 - Verifica a senha (SEMPRE, existindo o operador ou não)
                // O mesmo trabalho nos três casos de falha (e-mail inexistente, conta inativa, senha errada) para o tempo de
                // resposta não revelar quais e-mails estão cadastrados nem quais contas estão inativas.
                var passwordMatches = _passwordVerifier.Verify(@operator, request.Password ?? string.Empty);
                #endregion

                #region 06 - Decide (a mesma resposta nos três casos de falha)
                // Nada a registrar: a falha já foi contada na admissão (região 03).
                if (@operator is null || !@operator.IsActive || !passwordMatches) {
                    return new Response("Credenciais inválidas", 401);
                }
                #endregion

                var token = _tokenService.Generate(@operator);

                // Login com sucesso zera a contagem, o nível e qualquer bloqueio da conta.
                await _loginThrottle.ResetAsync(_tenantContext.TenantId, email.Address, cancellationToken);

                #region 07 - Retorna o usuário autenticado
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
