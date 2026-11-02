using System.Reflection;
using AccessControlSystem.Application.SharedContext.UseCases.Contracts;
using Flunt.Notifications;
using Flunt.Validations;

namespace AccessControlSystem.Tests.Architecture;

// FR-007 / Princípio IX ("Entrada"): toda string de Request tem tamanho máximo validado na Specification.
// O teste descobre os Request por reflexão: um campo novo sem limite quebra aqui sem precisar registrar nada à mão.
public class RequestTextLimitsTests
{
    // "Bem acima de qualquer limite" razoável de campo de formulário.
    private const int HugeLength = 100_000;

    private const string ContextNamespacePrefix = "AccessControlSystem.Application.Context.";

    // Trecho comum a toda mensagem de tamanho ("Senha deve ter no máximo 128 caracteres").
    private const string SizeMessageMarker = "no máximo";

    private static IEnumerable<Type> RequestTypes() =>
        typeof(IRequest).Assembly.GetTypes()
            .Where(t => t.Name == "Request" && typeof(IRequest).IsAssignableFrom(t) && !t.IsAbstract)
            .OrderBy(t => t.FullName, StringComparer.Ordinal);

    private static IEnumerable<PropertyInfo> TextProperties(Type request) =>
        request.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0);

    private static string Key(Type request, string property) =>
        $"{request.Namespace![ContextNamespacePrefix.Length..]}.{property}";

    private static MethodInfo? EnsureOf(Type request) =>
        request.Assembly.GetType($"{request.Namespace}.Specification")?
            .GetMethod("Ensure", BindingFlags.Public | BindingFlags.Static, [request]);

    // Duas colunas curtas (caso de uso e campo): o nome de exibição do xUnit trunca textos longos e linhas com o mesmo
    // nome truncado seriam fundidas pelo executor, escondendo falhas.
    public static IEnumerable<object[]> TextFields() =>
        RequestTypes().SelectMany(request =>
            TextProperties(request).Select(property =>
                new object[] { request.Namespace![ContextNamespacePrefix.Length..], property.Name }));

    // Monta o Request pelo construtor: só a propriedade alvo recebe o texto gigante, o resto recebe valores neutros.
    private static object Build(Type request, string targetProperty)
    {
        var constructor = request.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .First();

        var arguments = constructor.GetParameters().Select(parameter =>
        {
            if (parameter.ParameterType == typeof(string))
                return parameter.Name == targetProperty ? new string('a', HugeLength) : "a";

            var underlying = Nullable.GetUnderlyingType(parameter.ParameterType);
            if (underlying is not null) return null;   // anuláveis (ERole?, DateTime?, ...) ficam nulos

            if (parameter.ParameterType == typeof(Guid)) return Guid.NewGuid();
            if (parameter.ParameterType.IsEnum) return Enum.GetValues(parameter.ParameterType).GetValue(0);
            if (parameter.HasDefaultValue) return parameter.DefaultValue;

            return Activator.CreateInstance(parameter.ParameterType);
        }).ToArray();

        return constructor.Invoke(arguments);
    }

    [Fact]
    public void Descobre_os_campos_de_texto_conhecidos()
    {
        // Trava de sanidade: se a reflexão não achar nada, o teste principal "passaria" sem provar coisa alguma.
        var found = RequestTypes()
            .SelectMany(request => TextProperties(request).Select(property => Key(request, property.Name)))
            .ToHashSet();

        string[] expected =
        [
            "AccountContext.UseCases.Authenticate.Email",
            "AccountContext.UseCases.Authenticate.Password",
            "AccountContext.UseCases.Create.Email",
            "AccountContext.UseCases.Create.Password",
            "AccountContext.UseCases.Update.Email",
            "AccountContext.UseCases.Update.Password",
            "AccountContext.UseCases.UpdateMe.Email",
            "AccountContext.UseCases.UpdateMe.Password",
            "UserContext.Create.Name",
            "UserContext.Create.Telephone",
            "UserContext.Create.Password",
            "UserContext.Create.Rg",
            "UserContext.Create.Cpf",
            "UserContext.Create.Observation",
        ];

        Assert.Empty(expected.Except(found));
    }

    [Fact]
    public void Todo_request_tem_specification_com_ensure()
    {
        var withoutSpecification = RequestTypes()
            .Where(request => EnsureOf(request) is null)
            .Select(request => request.FullName)
            .ToList();

        Assert.True(
            withoutSpecification.Count == 0,
            "Request sem Specification.Ensure(Request) no mesmo namespace:" + Environment.NewLine
            + string.Join(Environment.NewLine, withoutSpecification));
    }

    [Theory]
    [MemberData(nameof(TextFields))]
    public void Campo_de_texto_acima_de_qualquer_limite_gera_notificacao_do_proprio_campo(string useCase, string property)
    {
        var requestTypeName = $"{ContextNamespacePrefix}{useCase}.Request";
        var request = typeof(IRequest).Assembly.GetType(requestTypeName)!;
        var ensure = EnsureOf(request);
        Assert.NotNull(ensure);

        var contract = (Contract<Notification>)ensure!.Invoke(null, [Build(request, property)])!;

        // Convenção das mensagens de tamanho: "<Campo> deve ter no máximo N caracteres". Só a chave não basta: uma
        // notificação de FORMATO (ex.: "E-mail inválido") também tem a chave do campo e não prova que há um limite.
        Assert.True(
            contract.Notifications.Any(n => n.Key == property && n.Message.Contains(SizeMessageMarker, StringComparison.Ordinal)),
            $"{requestTypeName}.{property} aceita texto de {HugeLength:N0} caracteres sem a notificação de tamanho " +
            $"(\"...{SizeMessageMarker}...\"): falta o limite de tamanho na Specification.");
    }
}
