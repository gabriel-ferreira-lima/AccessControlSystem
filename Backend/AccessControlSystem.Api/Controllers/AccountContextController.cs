using AccessControlSystem.Api.Extensions;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace AccessControlSystem.Api.Controllers {
    [ApiController]
    [Route("v1/account")]
    public class AccountContextController : ControllerBase {

        /// <summary>Autentica um operador e devolve o token JWT.</summary>
        /// <param name="request">
        /// <p>Para autenticar é necessário informar o e-mail e a senha do operador.</p>
        /// <hr/>
        /// <p><b>email:</b> E-mail cadastrado do operador <b>*obrigatório</b></p>
        /// <p><b>password:</b> Senha de acesso do operador <b>*obrigatório</b></p>
        /// </param>
        /// <response code="200">Login efetuado, token devolvido.</response>
        /// <response code="400">E-mail em formato inválido.</response>
        /// <response code="401">Credenciais inválidas ou conta desativada.</response>
        /// <response code="429">Muitas tentativas de login para este IP.</response>
        [EnableRateLimiting("login")]
        [HttpPost("login")]
        public async Task<IActionResult> Login(
            [FromBody] Application.Context.AccountContext.UseCases.Authenticate.Request request,
            [FromServices] IHandler<
                        Application.Context.AccountContext.UseCases.Authenticate.Request,
                        Application.Context.AccountContext.UseCases.Authenticate.Response> handler,
            CancellationToken cancellationToken) {
            var result = await handler.HandleAsync(request, cancellationToken);

            return StatusCode(result.Status, result);
        }

        /// <summary>Cria uma conta de operador. Só um admin pode chamar.</summary>
        /// <param name="request">
        /// <p>Para criar um operador é necessário informar e-mail, senha e o perfil de acesso.</p>
        /// <hr/>
        /// <p><b>email:</b> E-mail do novo operador <b>*obrigatório</b></p>
        /// <p><b>password:</b> Mínimo 8 caracteres, com maiúscula, minúscula, número e símbolo <b>*obrigatório</b></p>
        /// <p><b>role:</b> Perfil de acesso <b>*obrigatório</b><br/>
        /// <ul><li>1 - Admin</li><li>2 - Operator</li></ul></p>
        /// </param>
        /// <response code="201">Conta criada.</response>
        /// <response code="400">Requisição inválida (e-mail/senha fora do padrão, perfil inválido).</response>
        /// <response code="409">Já existe conta cadastrada com esse e-mail.</response>
        [Authorize(Roles = "Admin")]
        [HttpPost("create")]
        public async Task<IActionResult> CreateAccount(
            [FromBody] Application.Context.AccountContext.UseCases.Create.Request request,
            [FromServices] IHandler<
                Application.Context.AccountContext.UseCases.Create.Request,
                Application.Context.AccountContext.UseCases.Create.Response> handler,
            CancellationToken cancellationToken) {
            var result = await handler.HandleAsync(request, cancellationToken);

            return StatusCode(result.Status, result);
        }

        /// <summary>Devolve os dados do próprio operador autenticado.</summary>
        /// <response code="200">Dados do operador.</response>
        /// <response code="401">Sessão inválida (conta desativada ou removida) — faça login novamente.</response>
        [Authorize]
        [HttpGet("get-me")]
        public async Task<IActionResult> GetMe(
            [FromServices] IHandler<
                                Application.Context.AccountContext.UseCases.GetMe.Request,
                                Application.Context.AccountContext.UseCases.GetMe.Response> handler,
            CancellationToken cancellationToken) {

            var operatorId = Guid.Parse(User.Id());
            var request = new Application.Context.AccountContext.UseCases.GetMe.Request(operatorId);

            var result = await handler.HandleAsync(request, cancellationToken);
            return StatusCode(result.Status, result);
        }

        /// <summary>Lista operadores de forma paginada. Só admin.</summary>
        /// <param name="request">
        /// <p><b>size:</b> Itens por página, de 1 a 100 (padrão 10)</p>
        /// <p><b>page:</b> Número da página, começando em 1 (padrão 1)</p>
        /// <p><b>isActive:</b> Filtro de status (padrão All)<br/>
        /// <ul><li>0 - Inactive</li><li>1 - Active</li><li>2 - All</li></ul></p>
        /// </param>
        /// <response code="200">Página de operadores.</response>
        /// <response code="400">Page/Size/IsActive fora do intervalo aceito.</response>
        [Authorize(Roles = "Admin")]
        [HttpGet("get-list")]
        public async Task<IActionResult> GetAsync(
            [FromQuery] Application.Context.AccountContext.UseCases.Get.Request request,
            [FromServices] IHandler<
                    Application.Context.AccountContext.UseCases.Get.Request,
                    Application.Context.AccountContext.UseCases.Get.Response> handler,
            CancellationToken cancellationToken) {

            var result = await handler.HandleAsync(request, cancellationToken);

            return StatusCode(result.Status, result);
        }

        /// <summary>Atualiza o próprio operador autenticado (e-mail e/ou senha).</summary>
        /// <param name="request">
        /// <p>Informe ao menos um campo. O Id é sempre o do próprio token, o que vier no corpo é ignorado.</p>
        /// <hr/>
        /// <p><b>email:</b> Novo e-mail (opcional)</p>
        /// <p><b>password:</b> Nova senha (opcional): mínimo 8 caracteres, com maiúscula, minúscula, número e símbolo</p>
        /// </param>
        /// <response code="200">Conta atualizada.</response>
        /// <response code="400">Nenhum campo informado, ou e-mail/senha fora do padrão.</response>
        /// <response code="401">Sessão inválida (conta desativada ou removida) — faça login novamente.</response>
        /// <response code="409">Já existe conta cadastrada com esse e-mail.</response>
        [Authorize]
        [HttpPut("update-me")]
        public async Task<IActionResult> UpdateMe(
            [FromBody] Application.Context.AccountContext.UseCases.UpdateMe.Request request,
            [FromServices] IHandler<
                        Application.Context.AccountContext.UseCases.UpdateMe.Request,
                        Application.Context.AccountContext.UseCases.UpdateMe.Response> handler,
            CancellationToken cancellationToken) {

            var operatorId = Guid.Parse(User.Id());
            var result = await handler.HandleAsync(request with { Id = operatorId }, cancellationToken);

            return StatusCode(result.Status, result);
        }

        /// <summary>Atualiza um operador qualquer (e-mail, senha e/ou perfil de acesso). Só admin.</summary>
        /// <param name="id">Id do operador a atualizar (vem da rota; o Id no corpo é ignorado).</param>
        /// <param name="request">
        /// <p>Informe ao menos um campo.</p>
        /// <hr/>
        /// <p><b>email:</b> Novo e-mail (opcional)</p>
        /// <p><b>password:</b> Nova senha (opcional): mínimo 8 caracteres, com maiúscula, minúscula, número e símbolo</p>
        /// <p><b>role:</b> Novo perfil de acesso (opcional). Um admin nunca pode ser rebaixado para Operator<br/>
        /// <ul><li>1 - Admin</li><li>2 - Operator</li></ul></p>
        /// </param>
        /// <response code="200">Conta atualizada.</response>
        /// <response code="400">Nenhum campo informado, e-mail/senha fora do padrão, ou tentativa de rebaixar um admin para operador.</response>
        /// <response code="404">Operador não encontrado.</response>
        /// <response code="409">Já existe conta cadastrada com esse e-mail.</response>
        [Authorize(Roles = "Admin")]
        [HttpPut("update/{id:guid}")]
        public async Task<IActionResult> UpdateAccount(
            Guid id,
            [FromBody] Application.Context.AccountContext.UseCases.Update.Request request,
            [FromServices] IHandler<
                Application.Context.AccountContext.UseCases.Update.Request,
                Application.Context.AccountContext.UseCases.Update.Response> handler,
            CancellationToken cancellationToken) {

            var result = await handler.HandleAsync(request with { Id = id }, cancellationToken);

            return StatusCode(result.Status, result);
        }

        /// <summary>Reativa um operador desativado. Só admin.</summary>
        /// <param name="id">Id do operador a reativar.</param>
        /// <response code="200">Conta reativada.</response>
        /// <response code="400">Id não informado.</response>
        /// <response code="404">Operador não encontrado.</response>
        [Authorize(Roles = "Admin")]
        [HttpPut("activate/{id:guid}")]
        public async Task<IActionResult> ActivateAccount(
            Guid id,
            [FromServices] IHandler<
                        Application.Context.AccountContext.UseCases.Activate.Request,
                        Application.Context.AccountContext.UseCases.Activate.Response> handler,
            CancellationToken cancellationToken) {

            var request = new Application.Context.AccountContext.UseCases.Activate.Request(id);

            var result = await handler.HandleAsync(request, cancellationToken);
            return StatusCode(result.Status, result);
        }

        /// <summary>
        /// Inativa um operador. Só admin.
        /// </summary>
        /// <param name="id">Id do operador a desativar.</param>
        /// <response code="200">Conta desativada.</response>
        /// <response code="400">Id não informado, tentativa de autodesativação, ou é o último admin ativo.</response>
        /// <response code="404">Operador não encontrado.</response>
        [Authorize(Roles = "Admin")]
        [HttpPut("deactivate/{id:guid}")]
        public async Task<IActionResult> DeactivateAccount(
            Guid id,
            [FromServices] IHandler<
                Application.Context.AccountContext.UseCases.Deactivate.Request,
                Application.Context.AccountContext.UseCases.Deactivate.Response> handler,
            CancellationToken cancellationToken) {

            var adminId = Guid.Parse(User.Id());
            var request = new Application.Context.AccountContext.UseCases.Deactivate.Request(id, adminId);

            var result = await handler.HandleAsync(request, cancellationToken);
            return StatusCode(result.Status, result);
        }
    }
}
