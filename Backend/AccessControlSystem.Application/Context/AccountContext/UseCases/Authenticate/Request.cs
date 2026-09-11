using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using System;
using System.Collections.Generic;
using System.Text;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate {
    public record Request(string Email, string Password) : IRequest;
}
