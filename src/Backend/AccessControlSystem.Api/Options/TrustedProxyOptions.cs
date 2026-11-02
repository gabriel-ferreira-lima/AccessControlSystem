using System.Net;
using Microsoft.Extensions.Options;

namespace AccessControlSystem.Api.Options {

    /// <summary>
    /// Proxies cujos cabeçalhos <c>X-Forwarded-*</c> a Api aceita (seção <c>ForwardedHeaders</c>). De qualquer outra
    /// origem os cabeçalhos são ignorados e vale o endereço real da conexão. Vazias, ficam só os padrões do framework
    /// (loopback): nunca "qualquer origem". No compose local, o IP fixo do nginx.
    /// </summary>
    public class TrustedProxyOptions {

        public const string SectionName = "ForwardedHeaders";

        /// <summary>Endereços IP de proxies confiáveis (ex.: <c>172.31.250.10</c>).</summary>
        public string[] KnownProxies { get; set; } = [];

        /// <summary>Redes confiáveis em notação CIDR (ex.: <c>10.0.0.0/24</c>).</summary>
        public string[] KnownNetworks { get; set; } = [];
    }

    /// <summary>Falha a partida se um proxy ou uma rede não for um IP ou CIDR válido, citando a chave e o índice.</summary>
    public class TrustedProxyOptionsValidator : IValidateOptions<TrustedProxyOptions> {

        public ValidateOptionsResult Validate(string? name, TrustedProxyOptions options) {
            var failures = new List<string>();

            for (var i = 0; i < options.KnownProxies.Length; i++) {
                if (!IPAddress.TryParse(options.KnownProxies[i], out _)) {
                    failures.Add($"{TrustedProxyOptions.SectionName}:{nameof(TrustedProxyOptions.KnownProxies)}:{i} não é um endereço IP válido.");
                }
            }

            for (var i = 0; i < options.KnownNetworks.Length; i++) {
                if (!IPNetwork.TryParse(options.KnownNetworks[i], out _)) {
                    failures.Add($"{TrustedProxyOptions.SectionName}:{nameof(TrustedProxyOptions.KnownNetworks)}:{i} não é uma rede CIDR válida (ex.: 10.0.0.0/24).");
                }
            }

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
