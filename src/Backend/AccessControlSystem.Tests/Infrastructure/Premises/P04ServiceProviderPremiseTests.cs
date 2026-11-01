using AccessControlSystem.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AccessControlSystem.Tests.Infrastructure.Premises;

// P-04 (R-14): a connection string, o tenant e a instância do contexto não fazem o EF criar um
// provedor interno de serviços novo. Não precisa de banco: criar o provedor não abre conexão.
public class P04ServiceProviderPremiseTests
{
    [Fact]
    public void Contextos_de_bases_diferentes_nao_geram_provedores_demais()
    {
        // O EF avisa (ManyServiceProvidersCreatedWarning) a partir do 21º provedor. Aqui o aviso vira
        // exceção: se cada contexto criasse o seu, o 21º lançaria.
        for (var i = 0; i < 30; i++)
        {
            var builder = new DbContextOptionsBuilder<PremiseDbContext>();
            TenantDbContextOptions.Configure(
                builder,
                $"Host=base{i}.invalido;Port=5432;Database=banco{i};Username=acs_app;Password=senha-{i}");
            builder.ConfigureWarnings(w => w.Throw(CoreEventId.ManyServiceProvidersCreatedWarning));

            using var context = new PremiseDbContext(builder.Options, new FixedTenantContext(Guid.NewGuid()));

            // Acessar o Model força a criação do provedor de serviços do contexto.
            Assert.NotNull(context.Model);
        }
    }
}
