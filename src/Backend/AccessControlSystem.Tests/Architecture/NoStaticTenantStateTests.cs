using System.Reflection;
using System.Runtime.CompilerServices;
using AccessControlSystem.Application.SharedContext.Tenancy;
using AccessControlSystem.Infrastructure.Data;

namespace AccessControlSystem.Tests.Architecture;

// Princípio I / FR-013: nada de dado de tenant (nem de configuração, conexão ou cache) em estado
// estático. Numa Api que atende todos os tenants, um campo estático é compartilhado por todos eles.
public class NoStaticTenantStateTests
{
    // Única exceção aprovada: instâncias únicas e SEM estado dos interceptors de tenant. Eles descobrem o
    // tenant pelo contexto em uso (eventData.Context) e nunca guardam nada dele (P-04, research.md).
    // Qualquer outra entrada exige justificativa aqui e aprovação do dono do projeto.
    private static readonly HashSet<string> Allowed =
    [
        "AccessControlSystem.Infrastructure.Tenancy.TenantDbContextOptions.TransactionInterceptor",
        "AccessControlSystem.Infrastructure.Tenancy.TenantDbContextOptions.CommandInterceptor",
    ];

    private const BindingFlags AllStatic =
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static IEnumerable<string> StaticStateOf(Type type)
    {
        foreach (var field in type.GetFields(AllStatic))
        {
            if (field.IsLiteral) continue;   // const
            if (field.GetCustomAttribute<CompilerGeneratedAttribute>() is not null) continue;
            yield return $"{type.FullName}.{field.Name}";
        }

        foreach (var property in type.GetProperties(AllStatic))
        {
            // Propriedade estática automática gera um campo (já visto acima); sem campo, ainda é estado se tiver set.
            yield return $"{type.FullName}.{property.Name}";
        }
    }

    [Fact]
    public void Nenhum_campo_estatico_guarda_dado_de_tenant_em_application_e_infrastructure()
    {
        var assemblies = new[]
        {
            typeof(ITenantContext).Assembly,   // Application
            typeof(AppDbContext).Assembly,     // Infrastructure
        };

        var found = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .SelectMany(StaticStateOf)
            .Where(name => !Allowed.Contains(name))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            found.Count == 0,
            "Estado estático encontrado em Application/Infrastructure (proibido pelo Princípio I):"
            + Environment.NewLine + string.Join(Environment.NewLine, found));
    }

    [Fact]
    public void A_lista_permitida_so_tem_campos_que_existem()
    {
        // Evita que a exceção sobreviva ao campo que a justificava.
        var infrastructure = typeof(AppDbContext).Assembly;
        var existing = infrastructure.GetTypes()
            .SelectMany(t => t.GetFields(AllStatic).Select(f => $"{t.FullName}.{f.Name}"))
            .ToHashSet();

        Assert.All(Allowed, name => Assert.Contains(name, existing));
    }
}
