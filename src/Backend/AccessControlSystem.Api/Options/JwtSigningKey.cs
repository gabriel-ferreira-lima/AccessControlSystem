using System.Text;

namespace AccessControlSystem.Api.Options {

    /// <summary>
    /// Fonte única dos bytes da chave de assinatura do JWT. O texto configurado é usado como está, em UTF-8 (a Api
    /// não decodifica base64): o mesmo valor serve para validar o tamanho na partida, assinar o token e validar a
    /// assinatura. Medir o que de fato assina é o que a RFC 7518 (seção 3.2) exige para HS256: pelo menos 256 bits.
    /// </summary>
    public static class JwtSigningKey {

        /// <summary>Tamanho mínimo da chave, em bytes (256 bits).</summary>
        public const int MinimumBytes = 32;

        public static byte[] GetBytes(string key) => Encoding.UTF8.GetBytes(key);

        public static int GetByteCount(string key) => Encoding.UTF8.GetByteCount(key);
    }
}
