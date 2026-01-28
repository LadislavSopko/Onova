using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Onova.Internal.Extensions
{
    internal static class HttpClientExtensions
    {
        public static async Task<string> GetStringAsync(
            this HttpClient client,
            string requestUri,
            CancellationToken cancellationToken = default)
        {
            // Try curl.exe first (works on Win11), fallback to HttpClient (works on Win10)
            try
            {
                return await GetStringWithCurlAsync(client, requestUri);
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // curl.exe not found, fallback to HttpClient
            }

            // Fallback: standard HttpClient
            using var response = await client.GetAsync(requestUri, cancellationToken);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private static async Task<string> GetStringWithCurlAsync(HttpClient client, string requestUri)
        {
            string? authHeader = null;
            if (client.DefaultRequestHeaders.Authorization != null)
            {
                var auth = client.DefaultRequestHeaders.Authorization;
                authHeader = $"{auth.Scheme} {auth.Parameter}";
            }

            var psi = new ProcessStartInfo
            {
                FileName = "curl.exe",
                Arguments = authHeader != null
                    ? $"-s -H \"Authorization: {authHeader}\" \"{requestUri}\""
                    : $"-s \"{requestUri}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start curl.exe");

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            await Task.Run(() => process.WaitForExit());

            if (process.ExitCode != 0)
                throw new HttpRequestException($"curl failed ({process.ExitCode}): {error}");

            if (output.Contains("403") && output.Contains("Forbidden"))
                throw new HttpRequestException("Response status code does not indicate success: 403 (Forbidden).");

            return output;
        }

        public static async Task<JsonElement> ReadAsJsonAsync(
            this HttpContent content,
            CancellationToken cancellationToken = default)
        {
            using var stream = await content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream, default, cancellationToken);

            return document.RootElement.Clone();
        }

        public static async Task<JsonElement> GetJsonAsync(
            this HttpClient client,
            string requestUri,
            CancellationToken cancellationToken = default)
        {
            using var response = await client.GetAsync(
                requestUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            );

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsJsonAsync(cancellationToken);
        }

        public static async Task CopyToStreamAsync(
            this HttpContent content,
            Stream destination,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var length = content.Headers.ContentLength;
            using var source = await content.ReadAsStreamAsync();

            using var buffer = PooledBuffer.ForStream();

            var totalBytesCopied = 0L;
            int bytesCopied;
            do
            {
                bytesCopied = await source.CopyBufferedToAsync(destination, buffer.Array, cancellationToken);
                totalBytesCopied += bytesCopied;

                if (length != null)
                    progress?.Report(1.0 * totalBytesCopied / length.Value);
            } while (bytesCopied > 0);
        }
    }
}