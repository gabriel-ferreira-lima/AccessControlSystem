using AccessControlSystem.Domain.Contexts.AccountContext.Entities;
using AccessControlSystem.Domain.Contexts.AccountContext.Enums;
using AccessControlSystem.Domain.Contexts.AccountContext.ValueObjects;
using AccessControlSystem.Infrastructure.Context.AccountContext.UseCases.Authenticate;

namespace AccessControlSystem.Tests.Infrastructure.AccountContext.Authenticate;

// FR-014: o login executa UMA verificação de hash em todos os casos. Quando o operador não existe, a verificação é
// feita contra um hash fictício com o mesmo algoritmo e o mesmo custo de um hash real, para o tempo de resposta não
// revelar quais e-mails estão cadastrados. Testes rápidos, sem Docker.
public class PasswordVerifierTests
{
    private const string Senha = "Str0ng!Pass";

    private static Operator NewOperator() =>
        new(new Email("op@test.com"), new Password(Senha), ERole.Operator);

    [Fact]
    public void Operador_com_senha_correta_devolve_true()
    {
        Assert.True(new PasswordVerifier().Verify(NewOperator(), Senha));
    }

    [Fact]
    public void Operador_com_senha_errada_devolve_false()
    {
        Assert.False(new PasswordVerifier().Verify(NewOperator(), "Errada1!"));
    }

    [Fact]
    public void Operador_nulo_devolve_false()
    {
        // Mesmo com a senha "certa" de um operador qualquer: sem operador, nunca confere.
        Assert.False(new PasswordVerifier().Verify(null, Senha));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    public void Operador_nulo_nao_lanca_para_senha_vazia_ou_curta(string senha)
    {
        Assert.False(new PasswordVerifier().Verify(null, senha));
    }

    [Fact]
    public void Operador_nulo_nao_lanca_para_senha_muito_longa()
    {
        Assert.False(new PasswordVerifier().Verify(null, new string('x', 10_000)));
    }

    // O hash fictício precisa custar o mesmo que um hash real: mesmo número de segmentos, mesmas iterações (primeiro
    // segmento) e mesmos tamanhos de sal e de chave. Se a biblioteca mudar os parâmetros, o fictício acompanha (é
    // criado pelo mesmo Password) e este teste acusa qualquer divergência.
    [Fact]
    public void Hash_ficticio_tem_a_mesma_estrutura_de_um_hash_real()
    {
        var real = new Password(Senha).Hash.Split('.');
        var ficticio = new PasswordVerifier().Placeholder.Hash.Split('.');

        Assert.Equal(3, real.Length);
        Assert.Equal(real.Length, ficticio.Length);
        Assert.Equal(real[0], ficticio[0]);                                             // iterações do PBKDF2
        Assert.Equal(Convert.FromBase64String(real[1]).Length, Convert.FromBase64String(ficticio[1]).Length);   // sal
        Assert.Equal(Convert.FromBase64String(real[2]).Length, Convert.FromBase64String(ficticio[2]).Length);   // chave
    }

    [Theory]
    [InlineData("")]
    [InlineData("password")]
    [InlineData("Str0ng!Pass")]
    [InlineData("Admin!Dev2026")]
    public void Hash_ficticio_nao_confere_com_senhas_comuns(string senha)
    {
        Assert.False(new PasswordVerifier().Placeholder.Verify(senha));
    }

    [Fact]
    public void Hash_ficticio_e_diferente_de_um_hash_real()
    {
        Assert.NotEqual(new Password(Senha).Hash, new PasswordVerifier().Placeholder.Hash);
    }
}
