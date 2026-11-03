namespace AccessControlSystem.Api.Health {

    /// <summary>
    /// Metadado que só as duas rotas de saúde (<c>/health/live</c> e <c>/health/ready</c>) carregam. Faz o JWT ignorar o
    /// cabeçalho <c>Authorization</c> nelas (sem validar o token e, portanto, sem consultar sessão nem catálogo): a
    /// verificação "viva" nunca depende de nada e a "pronta" não perde a proteção do cache. Nenhum outro endpoint o tem.
    /// </summary>
    public sealed class HealthEndpointMarker {
    }
}
