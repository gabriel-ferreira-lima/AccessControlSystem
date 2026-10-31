using AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe.Contracts;
using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Exceptions;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe {
    public class Handler : IHandler<Request, Response> {

        private readonly IRepository _repository;

        public Handler(IRepository repository) {
            _repository = repository;
        }
        public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken = default) {
            try {

                #region 01 - Valida se a requisição esta correta
                var res = Specification.Ensure(request);

                if (!res.IsValid) {
                    return new Response("Requsição inválida", 400, res.Notifications);
                }
                #endregion

                #region 02 - Busca usuario no banco
                var @operator = await _repository.GetUserById(request.Id, cancellationToken);

                if (@operator == null) {
                    return new Response("Sessão invalida - faça o login novamente", 401);
                }
                #endregion

                #region 03 - Atualiza Email e senha
                if (request.Email != null) {
                    var newEmail = new Email(request.Email);

                    if (newEmail.Address != @operator.Email.Address) {
                        if (await _repository.CheckIfEmailIsNotUsed(newEmail.Address, request.Id, cancellationToken)) {
                            return new Response("Email já está em uso", 409);
                        }

                        @operator.UpdateEmail(newEmail);
                    }
                }

                if (request.Password != null) {
                    var newPassword = new Password(request.Password);
                    @operator.UpdatePassword(newPassword);
                }
                #endregion

                #region 04 - Atualiza informação no banco
                await _repository.UpdateAsync(@operator, cancellationToken);
                #endregion

                #region 05 - Retorna resposta
                return new Response("Usuario atualizado com sucesso", 200, new ResponseData(@operator.Id, @operator.Email));
                #endregion

            }
            catch (DomainException domainException) {
                return new Response(domainException.Message, 400);
            }
            catch (ConflictException conflictException) {
                return new Response(conflictException.Message, 409);
            }
        }
    }
}
