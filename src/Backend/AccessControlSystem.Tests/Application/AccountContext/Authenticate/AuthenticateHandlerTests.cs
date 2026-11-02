using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate;

namespace AccessControlSystem.Tests.Application.AccountContext.Authenticate;

public class AuthenticateHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly Handler _handler;

    public AuthenticateHandlerTests()
    {
        // Os testes de comportamento usam o verificador REAL; os de "o verificador foi chamado" montam o handler
        // com um IPasswordVerifier substituto (ver HandlerComVerificadorSubstituto).
        _handler = new Handler(_repository, _tokenService, new PasswordVerifier());
        _tokenService.Generate(Arg.Any<Operator>()).Returns("fake.jwt.token");
    }

    private static Operator OperatorWithPassword(string senha) =>
        new(new Email("op@test.com"), new Password(senha), ERole.Admin);

    [Fact]
    public async Task Requisicao_invalida_devolve_400_sem_consultar_o_banco()
    {
        var response = await _handler.HandleAsync(new Request("nope", "Str0ng!Pass"));

        Assert.Equal(400, response.Status);
        await _repository.DidNotReceive().GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Usuario_inexistente_devolve_401_sem_gerar_token()
    {
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Operator?)null);

        var response = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(401, response.Status);
        Assert.Equal("Credenciais inválidas", response.Message);
        _tokenService.DidNotReceive().Generate(Arg.Any<Operator>());
    }

    [Fact]
    public async Task Senha_errada_devolve_401_sem_gerar_token()
    {
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OperatorWithPassword("Str0ng!Pass"));

        var response = await _handler.HandleAsync(new Request("op@test.com", "Errada1!"));

        Assert.Equal(401, response.Status);
        _tokenService.DidNotReceive().Generate(Arg.Any<Operator>());
    }

    [Fact]
    public async Task Credenciais_corretas_devolvem_200_com_token_e_dados()
    {
        var op = OperatorWithPassword("Str0ng!Pass");
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);

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
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);

        await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        _tokenService.Received(1).Generate(op);
    }

    #region Verificação de senha em todos os casos (FR-014, FR-015)

    private readonly IPasswordVerifier _verifier = Substitute.For<IPasswordVerifier>();

    private Handler HandlerComVerificadorSubstituto() => new(_repository, _tokenService, _verifier);

    private static Operator InactiveOperator()
    {
        var op = OperatorWithPassword("Str0ng!Pass");
        op.Deactivate();
        return op;
    }

    [Fact]
    public async Task Verificador_e_chamado_quando_o_email_nao_existe()
    {
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Operator?)null);

        var response = await HandlerComVerificadorSubstituto().HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(401, response.Status);
        _verifier.Received(1).Verify(null, "Str0ng!Pass");
    }

    [Fact]
    public async Task Verificador_e_chamado_quando_a_conta_esta_inativa()
    {
        var op = InactiveOperator();
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);
        _verifier.Verify(op, "Str0ng!Pass").Returns(true);   // senha correta: mesmo assim a conta inativa é recusada

        var response = await HandlerComVerificadorSubstituto().HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(401, response.Status);
        _verifier.Received(1).Verify(op, "Str0ng!Pass");
    }

    [Fact]
    public async Task Verificador_e_chamado_quando_a_senha_esta_errada()
    {
        var op = OperatorWithPassword("Str0ng!Pass");
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);
        _verifier.Verify(op, "Errada1!").Returns(false);

        var response = await HandlerComVerificadorSubstituto().HandleAsync(new Request("op@test.com", "Errada1!"));

        Assert.Equal(401, response.Status);
        _verifier.Received(1).Verify(op, "Errada1!");
    }

    [Fact]
    public async Task Verificador_e_chamado_no_sucesso()
    {
        var op = OperatorWithPassword("Str0ng!Pass");
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);
        _verifier.Verify(op, "Str0ng!Pass").Returns(true);

        var response = await HandlerComVerificadorSubstituto().HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(200, response.Status);
        _verifier.Received(1).Verify(op, "Str0ng!Pass");
    }

    [Fact]
    public async Task Requisicao_invalida_nao_chama_o_verificador()
    {
        var response = await HandlerComVerificadorSubstituto().HandleAsync(new Request("nope", "Str0ng!Pass"));

        Assert.Equal(400, response.Status);
        _verifier.DidNotReceiveWithAnyArgs().Verify(default, default!);
    }

    // Guarda: já passa hoje (a conta inativa nunca recebe token).
    [Fact]
    public async Task Conta_inativa_com_senha_correta_devolve_401_sem_gerar_token()
    {
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(InactiveOperator());

        var response = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(401, response.Status);
        Assert.Equal("Credenciais inválidas", response.Message);
        _tokenService.DidNotReceive().Generate(Arg.Any<Operator>());
    }

    // Guarda: já passa hoje.
    [Fact]
    public async Task As_tres_falhas_devolvem_status_mensagem_e_notificacoes_identicos()
    {
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Operator?)null, InactiveOperator(), OperatorWithPassword("Str0ng!Pass"));

        var inexistente = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));
        var inativa = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));
        var senhaErrada = await _handler.HandleAsync(new Request("op@test.com", "Errada1!"));

        foreach (var falha in new[] { inativa, senhaErrada })
        {
            Assert.Equal(inexistente.Status, falha.Status);
            Assert.Equal(inexistente.Message, falha.Message);
            Assert.Equal(inexistente.Notifications, falha.Notifications);
            Assert.Null(falha.Data);
        }
    }

    // Decisão R-09: o e-mail é normalizado (sem espaços nas pontas e em minúsculas) antes da busca.
    [Fact]
    public async Task Email_e_normalizado_antes_da_busca()
    {
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Operator?)null);

        await _handler.HandleAsync(new Request("  Op@Test.COM ", "Str0ng!Pass"));

        await _repository.Received(1).GetOperatorByEmailAsync("op@test.com", Arg.Any<CancellationToken>());
    }

    #endregion
}
