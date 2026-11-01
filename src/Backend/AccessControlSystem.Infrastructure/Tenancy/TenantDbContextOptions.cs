using AccessControlSystem.Infrastructure.Data.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace AccessControlSystem.Infrastructure.Tenancy {

    /// <summary>
    /// Configuração única dos contextos "com tenant" (Api, Migrator em modo seed e testes): Npgsql e
    /// os interceptors que informam o tenant ao banco.
    /// </summary>
    public static class TenantDbContextOptions {

        // Instâncias únicas e SEM estado: o tenant vem do contexto em uso e nunca fica nelas (Princípio
        // I). A premissa P-04 mostrou que, no EF 10.0.12, instâncias novas de interceptor não criam
        // provedor de serviços novo; mesmo assim não há por que alocar um par por requisição.
        private static readonly TenantTransactionInterceptor TransactionInterceptor = new();
        private static readonly TenantCommandInterceptor CommandInterceptor = new();

        public static DbContextOptionsBuilder Configure(DbContextOptionsBuilder builder, string connectionString) {
            builder.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly("AccessControlSystem.Api"));
            builder.AddInterceptors(TransactionInterceptor, CommandInterceptor);
            return builder;
        }
    }
}
