using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;

namespace AccessControlSystem.Api.Health {

    public static class HealthEndpointExtensions {

        /// <summary>Nome da verificação do catálogo (a única que a rota "pronta" executa).</summary>
        public const string CatalogCheckName = "catalog";

        /// <summary>
        /// Mapeia as duas verificações de saúde da Api: <c>/health/live</c> (o processo atende; não consulta nada) e
        /// <c>/health/ready</c> (o catálogo responde; no máximo 1 consulta a cada 5 s por instância).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Não são casos de uso nem têm controller: usam o <c>MapHealthChecks</c> do framework, ficam fora de
        /// <c>v1/</c> (a seção "Operação" da constituição nomeia as duas rotas) e por isso não entram no
        /// <c>EndpointCatalog</c> nem no Swagger.
        /// </para>
        /// <para>
        /// São <b>anônimas</b> (<c>AllowAnonymous</c> explícito), só <c>GET</c> e <c>HEAD</c>, e a resposta é mínima:
        /// texto puro <c>Healthy</c> ou <c>Unhealthy</c> com <c>200</c> ou <c>503</c> e <c>Cache-Control: no-store</c>,
        /// sem detalhe interno. Carregam o <see cref="HealthEndpointMarker"/>, que faz o JWT ignorar o cabeçalho
        /// <c>Authorization</c> nelas (e só nelas).
        /// </para>
        /// </remarks>
        public static WebApplication MapHealthEndpoints(this WebApplication app) {
            var methods = new HttpMethodMetadata(["GET", "HEAD"]);
            var marker = new HealthEndpointMarker();

            app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
                .AllowAnonymous()
                .WithMetadata(methods, marker);

            app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Name == CatalogCheckName })
                .AllowAnonymous()
                .WithMetadata(methods, marker);

            return app;
        }
    }
}
