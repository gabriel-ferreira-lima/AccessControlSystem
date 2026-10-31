using AccessControlSystem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Api.Extensions {
    public static class MigrationExtensions {

        public static void ApplyMigrations(this WebApplication app) {
            using var scope = app.Services.CreateScope();
            var services = scope.ServiceProvider;
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");
            var database = services.GetRequiredService<AppDbContext>().Database;

            const int maxAttempts = 12;
            for (var attempt = 1; ; attempt++) {
                try {
                    database.Migrate();
                    logger.LogInformation("Migrations aplicadas com sucesso.");
                    return;
                }
                catch (Exception ex) when (attempt < maxAttempts) {
                    logger.LogWarning(
                        ex,
                        "Não foi possível aplicar as migrations (tentativa {Attempt}/{Max}). Nova tentativa em 5s.",
                        attempt, maxAttempts);
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }
            }
        }
    }
}
