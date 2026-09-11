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
