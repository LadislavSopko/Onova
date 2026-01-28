using System;
using System.Net;
using System.Net.Http;

namespace Onova.Internal
{
    internal static class Http
    {
        private static readonly Lazy<HttpClient> ClientLazy = new(() =>
        {
            var handler = new HttpClientHandler();

            if (handler.SupportsAutomaticDecompression)
                handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            handler.UseCookies = false;

            var httpClient = new HttpClient(handler, true);

            // Browser-like User-Agent for better compatibility with hosting providers
            httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");

            // Force HTTP/1.1 - fixes 403 errors on Win11 with some servers (e.g., SiteGround)
            // Win11 defaults to HTTP/2 which some shared hosting servers reject
            httpClient.DefaultRequestVersion = new Version(1, 1);
            httpClient.DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact;

            return httpClient;
        });

        public static HttpClient Client => ClientLazy.Value;
    }
}