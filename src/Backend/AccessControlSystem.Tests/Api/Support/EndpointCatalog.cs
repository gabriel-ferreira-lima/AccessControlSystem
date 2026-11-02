using System.Text.RegularExpressions;

namespace AccessControlSystem.Tests.Api.Support;

/// <summary>Quem pode chamar um endpoint.</summary>
public enum EndpointAccess
{
    /// <summary>Sem token (hoje só o login).</summary>
    Anonymous,

    /// <summary>Qualquer operador autenticado.</summary>
    Authenticated,

    /// <summary>Só o perfil de administrador (a regra por permissão vem na spec 08).</summary>
    AdminOnly
}

/// <summary>Um endpoint da Api: método HTTP, rota normalizada (<c>{id}</c> no lugar de <c>{id:guid}</c>) e acesso.</summary>
public sealed record CatalogEntry(string Method, string Route, EndpointAccess Access)
{
    public string Key => EndpointCatalog.Key(Method, Route);
}

/// <summary>Resultado da comparação entre o catálogo e as rotas reais da Api (FR-030).</summary>
public sealed record CatalogDiff(IReadOnlyList<string> RealRoutesOutsideCatalog, IReadOnlyList<string> CatalogEntriesWithoutRoute)
{
    public bool IsEmpty => RealRoutesOutsideCatalog.Count == 0 && CatalogEntriesWithoutRoute.Count == 0;

    public override string ToString() =>
        $"Rotas reais SEM cobertura no catálogo: [{string.Join("; ", RealRoutesOutsideCatalog)}]. " +
        $"Entradas do catálogo SEM rota real: [{string.Join("; ", CatalogEntriesWithoutRoute)}].";
}

/// <summary>
/// Lista oficial dos endpoints da Api e de quem pode chamá-los. Todo endpoint novo DEVE entrar aqui, e a suíte falha
/// (<c>EndpointCoverageTests</c>) quando uma rota real não está no catálogo ou o catálogo cita uma rota que não existe.
/// As teorias de <c>EndpointAuthorizationTests</c> são geradas a partir dele.
/// </summary>
public static class EndpointCatalog
{
    public static IReadOnlyList<CatalogEntry> Entries { get; } =
    [
        new("POST", "v1/account/login", EndpointAccess.Anonymous),
        new("GET", "v1/account/get-me", EndpointAccess.Authenticated),
        new("PUT", "v1/account/update-me", EndpointAccess.Authenticated),
        new("POST", "v1/account/create", EndpointAccess.AdminOnly),
        new("GET", "v1/account/get-list", EndpointAccess.AdminOnly),
        new("PUT", "v1/account/update/{id}", EndpointAccess.AdminOnly),
        new("PUT", "v1/account/activate/{id}", EndpointAccess.AdminOnly),
        new("PUT", "v1/account/deactivate/{id}", EndpointAccess.AdminOnly),
    ];

    /// <summary>Chave normalizada: método em maiúsculas, sem barra inicial, <c>{id:guid}</c> vira <c>{id}</c>.</summary>
    public static string Key(string method, string route)
    {
        var normalized = Regex.Replace(route.Trim().TrimStart('/'), @"\{(\w+)(:[^}]*)?\}", "{$1}");
        return $"{method.Trim().ToUpperInvariant()} {normalized}";
    }

    /// <param name="realRoutes">Rotas reais no formato <c>"METHOD rota"</c> (a rota pode ter restrições como <c>{id:guid}</c>).</param>
    public static CatalogDiff Diff(IEnumerable<string> realRoutes)
    {
        var real = realRoutes
            .Select(route => { var cut = route.IndexOf(' '); return Key(route[..cut], route[(cut + 1)..]); })
            .ToHashSet();
        var catalog = Entries.Select(entry => entry.Key).ToHashSet();

        return new CatalogDiff(
            real.Except(catalog).Order().ToArray(),
            catalog.Except(real).Order().ToArray());
    }
}
