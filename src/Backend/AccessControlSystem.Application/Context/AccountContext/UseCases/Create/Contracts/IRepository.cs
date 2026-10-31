using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AccessControlSystem.Application.Context.AccountContext.UseCases.Create.Contracts {
    public interface IRepository {

        Task<bool> AnyAsync(string email, CancellationToken cancellationToken = default);

        Task SaveAsync(Operator @operator, CancellationToken cancellationToken = default);
    }
}
