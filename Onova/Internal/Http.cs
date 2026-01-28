using System;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;

namespace Onova.Internal
{
    internal static class Http
    {
        private static readonly Lazy<HttpClient> ClientLazy = new(() =>
        {
            // Force TLS 1.2 - fixes 403 errors on Win11 with some servers (e.g., SiteGround)
            // Win11 defaults to TLS 1.3 which some shared hosting servers reject or misconfigure
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11;

            var handler = new HttpClientHandler
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11
            };

            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            handler.UseCookies = false;

            var httpClient = new HttpClient(handler, true);

            // Browser-like User-Agent - use TryAddWithoutValidation to prevent header parsing/splitting
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // Force HTTP/1.1 via reflection (netstandard2.0 compatible)
            TrySetHttp11(httpClient);

            return httpClient;
        });

        private static void TrySetHttp11(HttpClient httpClient)
        {
            try
            {
                // DefaultRequestVersion available in .NET Core 3.0+ / .NET 5+
                var versionProperty = typeof(HttpClient).GetProperty("DefaultRequestVersion");
                versionProperty?.SetValue(httpClient, new Version(1, 1));

                // DefaultVersionPolicy available in .NET 5+
                var policyProperty = typeof(HttpClient).GetProperty("DefaultVersionPolicy");
                if (policyProperty != null)
                {
                    // HttpVersionPolicy.RequestVersionExact = 1
                    var policyType = policyProperty.PropertyType;
                    var exactValue = Enum.ToObject(policyType, 1);
                    policyProperty.SetValue(httpClient, exactValue);
                }
            }
            catch
            {
                // Ignore - running on older runtime without these APIs
            }
        }

        public static HttpClient Client => ClientLazy.Value;
    }
}