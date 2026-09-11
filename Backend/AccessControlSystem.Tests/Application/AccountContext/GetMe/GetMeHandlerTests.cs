using AccessControlSystem.Application.Context.AccountContext.UseCases.GetMe;
using AccessControlSystem.Application.Context.AccountContext.UseCases.GetMe.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

namespace AccessControlSystem.Tests.Application.AccountContext.GetMe;

public class GetMeHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly Handler _handler;

    public GetMeHandlerTests()
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
    public async Task Operador_nao_encontrado_devolve_401()
    {
        _repository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Operator?)null);

        var response = await _handler.HandleAsync(new Request(Guid.NewGuid()));

        Assert.Equal(401, response.Status);
    }

    [Fact]
    public async Task Devolve_200_com_os_dados_do_operador()
    {
        var op = new Operator(new Email("op@test.com"), new Password("Str0ng!Pass"), ERole.Admin);
        _repository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(op);

        var response = await _handler.HandleAsync(new Request(op.Id));

        Assert.Equal(200, response.Status);
        Assert.Equal(op.Id, response.Data!.Id);
        Assert.Equal("op@test.com", response.Data.Email);
        Assert.Equal(ERole.Admin, response.Data.Role);
    }
}
