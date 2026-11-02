using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate;
using AccessControlSystem.Application.Context.AccountContext.UseCases.Authenticate.Contracts;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Application.SharedContext.UseCases.Services;
using AccessControlSystem.Domain.SharedContext.Exceptions;
using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate;

namespace AccessControlSystem.Tests.Application.AccountContext.Authenticate;

public class AuthenticateHandlerTests
{
    private readonly IRepository _repository = Substitute.For<IRepository>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly ILoginThrottle _loginThrottle = Substitute.For<ILoginThrottle>();
    private readonly ITenantContext _tenantContext = Substitute.For<ITenantContext>();
    private readonly Handler _handler;

    private static readonly Guid TenantId = Guid.Parse("d1d1d1d1-0000-4000-8000-000000000001");

    public AuthenticateHandlerTests()
    {
        // Por padrão o limitador ADMITE a tentativa (devolve null) e o tenant do contexto é fixo; os testes do limite
        // por conta mudam isso. Os testes de comportamento usam o verificador REAL; os de "o verificador foi chamado"
        // montam o handler com um IPasswordVerifier substituto (ver HandlerComVerificadorSubstituto).
        _loginThrottle.TryAcquireAttemptAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TimeSpan?)null);
        _tenantContext.IsResolved.Returns(true);
        _tenantContext.TenantId.Returns(TenantId);

        _handler = new Handler(_repository, _tokenService, new PasswordVerifier(), _loginThrottle, _tenantContext);
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

    private Handler HandlerComVerificadorSubstituto() => new(_repository, _tokenService, _verifier, _loginThrottle, _tenantContext);

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

    #region Limite por conta (FR-017 a FR-022)

    private static readonly TimeSpan Espera = TimeSpan.FromSeconds(60);

    private void ContaBloqueada() =>
        _loginThrottle.TryAcquireAttemptAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((TimeSpan?)Espera);

    [Fact]
    public async Task Conta_bloqueada_devolve_429_com_retry_after_sem_consultar_banco_nem_verificar_senha()
    {
        ContaBloqueada();

        var response = await HandlerComVerificadorSubstituto().HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(429, response.Status);
        Assert.Equal(Espera, response.RetryAfter);
        Assert.Null(response.Data);
        await _repository.DidNotReceiveWithAnyArgs().GetOperatorByEmailAsync(default!, default);
        _verifier.DidNotReceiveWithAnyArgs().Verify(default, default!);
        _tokenService.DidNotReceive().Generate(Arg.Any<Operator>());
    }

    [Fact]
    public async Task Mensagem_do_429_e_a_mesma_do_limite_por_ip()
    {
        ContaBloqueada();

        var response = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal("Muitas tentativas. Tente novamente em instantes.", response.Message);
    }

    [Fact]
    public async Task Tentativa_admitida_segue_para_a_busca_e_a_verificacao()
    {
        var op = OperatorWithPassword("Str0ng!Pass");
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);
        _verifier.Verify(op, "Errada1!").Returns(false);

        await HandlerComVerificadorSubstituto().HandleAsync(new Request("op@test.com", "Errada1!"));

        await _repository.Received(1).GetOperatorByEmailAsync("op@test.com", Arg.Any<CancellationToken>());
        _verifier.Received(1).Verify(op, "Errada1!");
    }

    [Fact]
    public async Task Sucesso_chama_reset_uma_vez()
    {
        var op = OperatorWithPassword("Str0ng!Pass");
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);

        var response = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(200, response.Status);
        await _loginThrottle.Received(1).ResetAsync(TenantId, "op@test.com", Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("email inexistente")]
    [InlineData("conta inativa")]
    [InlineData("senha errada")]
    public async Task Falhas_nao_chamam_reset(string caso)
    {
        Operator? op = caso switch
        {
            "email inexistente" => null,
            "conta inativa" => InactiveOperator(),
            _ => OperatorWithPassword("Str0ng!Pass")
        };
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(op);
        var senha = caso == "senha errada" ? "Errada1!" : "Str0ng!Pass";

        var response = await _handler.HandleAsync(new Request("op@test.com", senha));

        Assert.Equal(401, response.Status);
        await _loginThrottle.DidNotReceiveWithAnyArgs().ResetAsync(default, default!, default);
    }

    [Fact]
    public async Task Requisicao_invalida_nao_chama_o_throttle()
    {
        var response = await _handler.HandleAsync(new Request("nope", "Str0ng!Pass"));

        Assert.Equal(400, response.Status);
        await _loginThrottle.DidNotReceiveWithAnyArgs().TryAcquireAttemptAsync(default, default!, default);
    }

    [Fact]
    public async Task Throttle_recebe_o_tenant_do_contexto_e_o_email_normalizado()
    {
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((Operator?)null);

        await _handler.HandleAsync(new Request("  Op@Test.COM ", "Str0ng!Pass"));

        await _loginThrottle.Received(1).TryAcquireAttemptAsync(TenantId, "op@test.com", Arg.Any<CancellationToken>());
    }

    // O handler captura DomainException (Princípio V) e devolve 400. Este caminho já existe desde a fase D.
    [Fact]
    public async Task Excecao_de_dominio_do_email_devolve_400()
    {
        _repository.GetOperatorByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Operator?>(_ => throw new DomainException("E-mail inválido"));

        var response = await _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass"));

        Assert.Equal(400, response.Status);
        Assert.Equal("E-mail inválido", response.Message);
    }

    // FR-022: se o limitador falhar, o login NÃO é liberado: a exceção sobe (na Api vira 500), sem token e sem ler o banco.
    [Fact]
    public async Task Falha_do_throttle_nao_gera_token()
    {
        _loginThrottle.TryAcquireAttemptAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<TimeSpan?>(_ => throw new InvalidOperationException("throttle indisponível"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.HandleAsync(new Request("op@test.com", "Str0ng!Pass")));

        _tokenService.DidNotReceive().Generate(Arg.Any<Operator>());
        await _repository.DidNotReceiveWithAnyArgs().GetOperatorByEmailAsync(default!, default);
    }

    #endregion
}
