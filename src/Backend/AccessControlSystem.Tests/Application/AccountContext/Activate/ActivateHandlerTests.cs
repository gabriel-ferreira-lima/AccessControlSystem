using AccessControlSystem.Application.Context.AccountContext.UseCases.Activate;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Activate.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

namespace AccessControlSystem.Tests.Application.AccountContext.Activate;

public class ActivateHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly Handler _handler;

    public ActivateHandlerTests()
    {
        _handler = new Handler(_repository);
    }

    [Fact]
    public async Task Requisicao_invalida_devolve_400_sem_tocar_no_banco()
    {
        var response = await _handler.HandleAsync(new Request(Guid.Empty));

        Assert.Equal(400, response.Status);
        await _repository.DidNotReceive().GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Operador_inexistente_devolve_404()
    {
        _repository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Operator?)null);

        var response = await _handler.HandleAsync(new Request(Guid.NewGuid()));

        Assert.Equal(404, response.Status);
    }

    [Fact]
    public async Task Caminho_feliz_reativa_e_persiste()
    {
        var op = new Operator(new Email("op@test.com"), new Password("Str0ng!Pass"), ERole.Operator);
        op.Deactivate();
        _repository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(op);

        var response = await _handler.HandleAsync(new Request(op.Id));

        Assert.Equal(200, response.Status);
        Assert.True(op.IsActive);
        Assert.True(response.Data!.IsActive);
        await _repository.Received(1).ActivateAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>());
    }
}
