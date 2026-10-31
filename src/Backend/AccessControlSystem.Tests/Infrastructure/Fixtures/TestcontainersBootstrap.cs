using System.Runtime.CompilerServices;

namespace AccessControlSystem.Tests.Infrastructure.Fixtures;

internal static class TestcontainersBootstrap
{
    [ModuleInitializer]
    internal static void Init()
    {
        // Desliga o "Ryuk" (container de limpeza do Testcontainers): em ambientes sem
        // acesso ao registry pra baixá-lo, ele quebra a subida. O PostgresContainerFixture
        // já descarta o container no DisposeAsync.
        Environment.SetEnvironmentVariable("TESTCONTAINERS_RYUK_DISABLED", "true");
    }
}
