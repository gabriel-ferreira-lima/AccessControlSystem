using AccessControlSystem.Migrator;

// Todo o comportamento fica em MigratorApplication, que é pública para os testes chamarem
// diretamente. O ponto de entrada tem nome próprio (e não o Program de nível superior) para não
// colidir com o Program da Api, que o projeto de testes também referencia.
internal static class MigratorEntryPoint {

    private static Task<int> Main(string[] args) => MigratorApplication.RunAsync(args);
}
