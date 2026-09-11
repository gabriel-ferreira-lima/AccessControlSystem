using AccessControlSystem.Application.Context.AccountContext.UseCases.Create;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Create.Contracts;
using AccessControlSystem.Application.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using NSubstitute.ExceptionExtensions;

namespace AccessControlSystem.Tests.Application.AccountContext.Create;

public class CreateHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly Handler _handler;

    public CreateHandlerTests()
    {
        _handler = new Handler(_repository);
        _repository.AnyAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
    }

    private static Request Valid() => new("user@test.com", "Str0ng!Pass", ERole.Operator);

    [Fact]
    public async Task Requisicao_invalida_devolve_400_e_nao_toca_no_repositorio()
    {
        var response = await _handler.HandleAsync(Valid() with { Email = "nope" });

        Assert.Equal(400, response.Status);
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Senha_fraca_devolve_400_sem_persistir()
    {
        var response = await _handler.HandleAsync(Valid() with { Password = "abcdefghij" });

        Assert.Equal(400, response.Status);
        Assert.NotEmpty(response.Notifications!);
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Email_ja_existente_devolve_409_sem_persistir()
    {
        _repository.AnyAsync(Arg.Is("user@test.com"), Arg.Any<CancellationToken>()).Returns(true);

        var response = await _handler.HandleAsync(Valid());

        Assert.Equal(409, response.Status);
        await _repository.DidNotReceive().SaveAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Conflito_de_corrida_no_banco_vira_409()
    {
        _repository
            .SaveAsync(Arg.Any<Operator>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ConflictException("E-mail já está em uso"));

        var response = await _handler.HandleAsync(Valid());

        Assert.Equal(409, response.Status);
        Assert.Equal("E-mail já está em uso", response.Message);
    }

    [Fact]
    public async Task Caminho_feliz_devolve_201_com_os_dados()
    {
        var response = await _handler.HandleAsync(Valid());

        Assert.Equal(201, response.Status);
        Assert.NotNull(response.Data);
        Assert.NotEqual(Guid.Empty, response.Data!.Id);
        Assert.Equal("user@test.com", response.Data.Email);
        Assert.Equal(ERole.Operator, response.Data.Role);
    }

    [Fact]
    public async Task Checa_existencia_usando_o_email_normalizado()
    {
        // e-mail em maiúsculas passa no Specification; o VO Email normaliza pra minúsculas
        await _handler.HandleAsync(Valid() with { Email = "USER@Test.com" });

        await _repository.Received(1).AnyAsync(Arg.Is("user@test.com"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Caminho_feliz_persiste_o_operador()
    {
        await _handler.HandleAsync(Valid());

        await _repository.Received(1).SaveAsync(
            Arg.Is<Operator>(o => o.Email.Address == "user@test.com" && o.Role == ERole.Operator),
            Arg.Any<CancellationToken>());
    }
}
