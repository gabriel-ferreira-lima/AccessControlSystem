using Microsoft.Extensions.Configuration;

namespace AccessControlSystem.Migrator {

    /// <summary>
    /// Ponto de entrada testável do Migrator. O <c>Program.cs</c> só delega a esta classe.
    /// </summary>
    public static class MigratorApplication {

        // ESQUELETO (T030): o roteamento dos verbos e dos códigos de saída vem na fase da US5.
        public static Task<int> RunAsync(
            string[] args,
            Action<IConfigurationBuilder>? configure = null,
            CancellationToken cancellationToken = default) {
            throw new NotImplementedException();
        }
    }
}
