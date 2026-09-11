using AccessControlSystem.Application.Context.AccountContext.UseCases.Deactivate;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Deactivate.Contracts;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

namespace AccessControlSystem.Tests.Application.AccountContext.Deactivate;

public class DeactivateHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly Handler _handler;

    public DeactivateHandlerTests()
    {
        _handler = new Handler(_repository);
    }

    private Operator GivenExistingOperator(ERole role) {
        var op = new Operator(new Email("op@test.com"), new Password("Str0ng!Pass"), role);
        _repository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(op);
        return op;
    }

    [Fact]
    public async Task Autodesativacao_devolve_400_sem_tocar_no_banco()
    {
        var id = Guid.NewGuid();

        var response = await _handler.HandleAsync(new Request(id, id));

        Assert.Equal(400, response.Status);
        await _repository.DidNotReceive().GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Operador_inexistente_devolve_404()
    {
        _repository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Operator?)null);

        var response = await _handler.HandleAsync(new Request(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(404, response.Status);
    }

    [Fact]
    public async Task Ultimo_admin_ativo_nao_pode_ser_desativado()
    {
        var admin = GivenExistingOperator(ERole.Admin);
        _repository.IsLastActiveAdminAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);

        var response = await _handler.HandleAsync(new Request(admin.Id, Guid.NewGuid()));

        Assert.Equal(400, response.Status);
        Assert.True(admin.IsActive);
        await _repository.DidNotReceive().DeactivateAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Admin_que_nao_e_o_ultimo_e_desativado()
    {
        var admin = GivenExistingOperator(ERole.Admin);
        _repository.IsLastActiveAdminAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(false);

        var response = await _handler.HandleAsync(new Request(admin.Id, Guid.NewGuid()));

        Assert.Equal(200, response.Status);
        Assert.False(admin.IsActive);
        Assert.False(response.Data!.IsActive);
    }

    [Fact]
    public async Task Operador_comum_nao_checa_ultimo_admin()
    {
        var op = GivenExistingOperator(ERole.Operator);

        var response = await _handler.HandleAsync(new Request(op.Id, Guid.NewGuid()));

        Assert.Equal(200, response.Status);
        await _repository.DidNotReceive().IsLastActiveAdminAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Caminho_feliz_persiste_e_devolve_os_dados()
    {
        var op = GivenExistingOperator(ERole.Operator);

        var response = await _handler.HandleAsync(new Request(op.Id, Guid.NewGuid()));

        Assert.Equal(200, response.Status);
        Assert.Equal(op.Id, response.Data!.Id);
        Assert.Equal("op@test.com", response.Data.Email);
        Assert.False(response.Data.IsActive);
        await _repository.Received(1).DeactivateAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>());
    }
}
