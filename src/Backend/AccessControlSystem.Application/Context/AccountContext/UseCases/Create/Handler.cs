using AccessControlSystem.Application.Context.AccountContext.UseCases.Create.Contracts;
using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Domain.SharedContext.Exceptions;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Create {
    public class Handler : IHandler<Request, Response> {

        private readonly IRepository _Repository;

        public Handler(IRepository repository) {
            _Repository = repository;
        }

        public async Task<Response> HandleAsync(Request request, CancellationToken cancellationToken = default) {

            try {
                #region 01 - Valida se a requisição esta correta

                var res = Specification.Ensure(request);
                if (!res.IsValid) {
                    return new Response("Requisição inválida", 400, res.Notifications);
                }
                #endregion

                #region 02 - Cria os objetos
                Email email;
                Password password;
                Operator @operator;

                email = new Email(request.Email);
                password = new Password(request.Password);
                @operator = new Operator(email, password, request.Role);
                #endregion

                #region 03 - Verifica se o Email já existe no banco de dados
                var exists = await _Repository.AnyAsync(email, cancellationToken);

                if (exists) {
                    return new Response("Email já está em uso", 409);
                }
                #endregion

                #region 04 - Persiste os dados no banco de dados
                await _Repository.SaveAsync(@operator, cancellationToken);
                #endregion

                #region 05 - Retorna a resposta
                return new Response("Operador criado com sucesso", 201,
                    new ResponseData(@operator.Id, @operator.Email, @operator.Role)
                );
                #endregion

            }
            catch (DomainException domainexception) {
                return new Response(domainexception.Message, 400);

            }
            catch(ConflictException conflictexception) {
                return new Response(conflictexception.Message, 409);
            }
        }
    }
}
