using Xunit;

namespace AccessControlSystem.Tests.T066;

// T066 caso 4: teste quebrado DE PROPÓSITO para provar que o ci-gate reprova. Nunca entra na main.
public class QuebradoDePropositoTests {

    [Fact]
    public void Teste_quebrado_de_proposito_para_o_gate_reprovar() {
        Assert.Fail("quebrado de propósito (T066 caso 4)");
    }
}
