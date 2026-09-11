using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;

namespace AccessControlSystem.Tests.Application.AccountContext.Authenticate;

public class AuthenticateHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly Handler _handler;

    public AuthenticateHandlerTests()
    {
        _handler = new Handler(_repository, _tokenService);
        _tokenService.Generate(Arg.Any<Operator>()).Returns("fake.jwt.token");
    }

    private static Operator OperatorWithPassword(string senha) =>
        new(new Email("op@test.com"), new Password(senha), ERole.Admin);

    [Fact]
    public async Task Requisicao_invalida_devolve_400_sem_consultar_o_banco()
    {
        var response = await _handler.HandleAsync(new Request("nope", "Str0ng!Pass"));

        Assert.Equal(400, response.Status);
        await _repository.DidNotReceive().GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Usuario_inexistente_devolve_401_sem_gerar_token()
    {
        _repository.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Operator?)null);

        var response = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(401, response.Status);
        Assert.Equal("Credenciais inválidas", response.Message);
        _tokenService.DidNotReceive().Generate(Arg.Any<Operator>());
    }

    [Fact]
    public async Task Senha_errada_devolve_401_sem_gerar_token()
    {
        _repository.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OperatorWithPassword("Str0ng!Pass"));

        var response = await _handler.HandleAsync(new Request("op@test.com", "Errada1!"));

        Assert.Equal(401, response.Status);
        _tokenService.DidNotReceive().Generate(Arg.Any<Operator>());
    }

    [Fact]
    public async Task Credenciais_corretas_devolvem_200_com_token_e_dados()
    {
        var op = OperatorWithPassword("Str0ng!Pass");
        _repository.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);

        var response = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(200, response.Status);
        Assert.NotNull(response.Data);
        Assert.Equal("fake.jwt.token", response.Data!.Token);
        Assert.Equal(op.Id, response.Data.Id);
        Assert.Equal("op@test.com", response.Data.Email);
        Assert.Equal(ERole.Admin, response.Data.Role);
    }

    [Fact]
    public async Task Gera_o_token_para_o_operador_autenticado()
    {
        var op = OperatorWithPassword("Str0ng!Pass");
        _repository.GetUserByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);

        await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        _tokenService.Received(1).Generate(op);
    }
}
