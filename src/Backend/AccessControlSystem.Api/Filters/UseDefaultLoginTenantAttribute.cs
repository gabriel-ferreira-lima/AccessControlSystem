namespace AccessControlSystem.Api.Filters {

    /// <summary>
    /// Marca o endpoint de login: ele não autentica por token e resolve o tenant padrão da
    /// configuração (<c>Login:DefaultTenantId</c>) antes do model binding.
    /// </summary>
    /// <remarks>
    /// Por enquanto é só um marcador, sem lógica (T008). O filtro de recurso completo vem na fase
    /// da US2.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public sealed class UseDefaultLoginTenantAttribute : Attribute {
    }
}
