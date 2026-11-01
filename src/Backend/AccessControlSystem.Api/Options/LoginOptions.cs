namespace AccessControlSystem.Api.Options {

    /// <summary>
    /// Configuração do login (seção <c>Login</c>). O tenant padrão existe só até a spec do login com
    /// escolha de tenant. Sem validação na partida: se faltar, o login responde <c>401</c> e o motivo
    /// vai para o log (caso de borda da spec).
    /// </summary>
    public class LoginOptions {

        public const string SectionName = "Login";

        public Guid? DefaultTenantId { get; set; }
    }
}
