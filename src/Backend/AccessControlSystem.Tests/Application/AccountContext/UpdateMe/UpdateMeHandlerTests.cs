using AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe;
using AccessControlSystem.Application.Context.AccountContext.UseCases.UpdateMe.Contracts;
using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using NSubstitute.ExceptionExtensions;

namespace AccessControlSystem.Tests.Application.AccountContext.UpdateMe;

public class UpdateMeHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly Handler _handler;

    public UpdateMeHandlerTests()
    {
        _handler = new Handler(_repository);
        _repository.CheckIfEmailIsNotUsed(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);
    }

    private Operator GivenExistingOperator(string email = "op@test.com")
    {
        var op = new Operator(new Email(email), new Password("Str0ng!Pass"), ERole.Operator);
        _repository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(op);
        return op;
    }

    [Fact]
    public async Task Requisicao_invalida_devolve_400_sem_tocar_no_banco()
    {
        var response = await _handler.HandleAsync(new Request(Guid.Empty));

        Assert.Equal(400, response.Status);
        await _repository.DidNotReceive().GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Sessao_sem_operador_correspondente_devolve_401()
    {
        _repository.GetUserById(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Operator?)null);

        var response = await _handler.HandleAsync(new Request(Guid.NewGuid(), Email: "novo@test.com"));

        Assert.Equal(401, response.Status);
    }

    [Fact]
    public async Task Email_em_uso_por_outro_devolve_409()
    {
        var op = GivenExistingOperator(email: "op@test.com");
        _repository.CheckIfEmailIsNotUsed(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var response = await _handler.HandleAsync(new Request(op.Id, Email: "novo@test.com"));

        Assert.Equal(409, response.Status);
        Assert.Equal("op@test.com", op.Email.Address);
        await _repository.DidNotReceive().UpdateAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Email_livre_troca_e_devolve_200()
    {
        var op = GivenExistingOperator(email: "op@test.com");

        var response = await _handler.HandleAsync(new Request(op.Id, Email: "NOVO@test.com"));

        Assert.Equal(200, response.Status);
        Assert.Equal("novo@test.com", op.Email.Address);
        Assert.Equal("novo@test.com", response.Data!.Email);
    }

    [Fact]
    public async Task Email_igual_ao_atual_nao_checa_duplicidade()
    {
        var op = GivenExistingOperator(email: "op@test.com");

        var response = await _handler.HandleAsync(new Request(op.Id, Email: "OP@test.com"));

        Assert.Equal(200, response.Status);
        await _repository.DidNotReceive()
            .CheckIfEmailIsNotUsed(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Troca_a_senha()
    {
        var op = GivenExistingOperator();
        var hashAntes = op.Password.Hash;

        var response = await _handler.HandleAsync(new Request(op.Id, Password: "NovaSenh4!"));

        Assert.Equal(200, response.Status);
        Assert.NotEqual(hashAntes, op.Password.Hash);
        Assert.True(op.Password.Verify("NovaSenh4!"));
    }

    [Fact]
    public async Task Conflito_de_corrida_no_save_devolve_409()
    {
        var op = GivenExistingOperator();
        _repository.UpdateAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException("E-mail já está em uso"));

        var response = await _handler.HandleAsync(new Request(op.Id, Email: "outro@test.com"));

        Assert.Equal(409, response.Status);
    }

    [Fact]
    public async Task Caminho_feliz_persiste_e_devolve_os_dados()
    {
        var op = GivenExistingOperator("op@test.com");

        var response = await _handler.HandleAsync(
            new Request(op.Id, Email: "novo@test.com", Password: "NovaSenh4!"));

        Assert.Equal(200, response.Status);
        Assert.Equal(op.Id, response.Data!.Id);
        Assert.Equal("novo@test.com", response.Data.Email);
        await _repository.Received(1).UpdateAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>());
    }
}
