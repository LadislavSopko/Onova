using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Onova.Exceptions;
using Onova.Internal;
using Onova.Internal.Extensions;

namespace Onova.Services
{

    /// <summary>
    /// Resolves packages using a manifest served by a web server.
    /// Manifest consists of package versions and URLs, separated by space, one line per version.
    /// </summary>
    public class WebPackageResolver : IPackageResolver
    {
        private readonly HttpClient _httpClient;
        private readonly string _manifestUrl;

        /// <summary>
        /// Initializes an instance of <see cref="WebPackageResolver"/>.
        /// </summary>
        public WebPackageResolver(HttpClient httpClient, string manifestUrl)
        {
            _httpClient = httpClient;
            _manifestUrl = manifestUrl;
        }

        /// <summary>
        /// Initializes an instance of <see cref="WebPackageResolver"/>.
        /// </summary>
        public WebPackageResolver(string manifestUrl)
            : this(Http.Client, manifestUrl)
        {
        }

        private string ExpandRelativeUrl(string url)
        {
            var manifestUri = new Uri(_manifestUrl);
            var uri = new Uri(manifestUri, url);

            return uri.ToString();
        }

        private async Task<IReadOnlyDictionary<Version, VersionWithInfo>> GetPackageVersionUrlMapAsync(CancellationToken cancellationToken)
        {
            var map = new Dictionary<Version, VersionWithInfo>();

            // Get manifest
            string response;
            response = await _httpClient.GetStringAsync(_manifestUrl, cancellationToken);
            

            foreach (var line in response.Split("\n"))
            {
                // Get package version and URL
                var versionText = line.SubstringUntil(" ").Trim();

                // 31/01/2023 laco #567 added posibility to have small release version note
                // it can be added in end of version line   <version url {Note....}>\n ....
                var url = line.SubstringAfter(" ").Trim();
                string note = "";

                var parts = url.Split("{");
                if(parts.Count() > 1)
                {
                    // we have NOTE
                    url = parts[0].Trim();
                    note = parts[1].Trim('{', '}', ' ');
                } else
                {
                    url = parts[0].Trim();
                }


                // If either is not set - skip
                if (string.IsNullOrWhiteSpace(versionText) || string.IsNullOrWhiteSpace(url))
                    continue;

                // Try to parse version
                if (!Version.TryParse(versionText, out var version))
                    continue;

                // Expand URL if it's relative
                url = ExpandRelativeUrl(url);

                // Add to dictionary
                map[version] = new VersionWithInfo(version, url, note);
            }

            

            return map;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<VersionWithInfo>> GetPackageVersionsAsync(CancellationToken cancellationToken = default)
        {
            var versions = await GetPackageVersionUrlMapAsync(cancellationToken);
            return versions.Values.ToArray();
        }

        /// <inheritdoc />
        public async Task DownloadPackageAsync(Version version, string destFilePath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Get map
            var map = await GetPackageVersionUrlMapAsync(cancellationToken);

            // Try to get package URL
            var packageInfo = map.GetValueOrDefault(version);
            if (string.IsNullOrWhiteSpace(packageInfo.Data))
                throw new PackageNotFoundException(version);

            // Try curl.exe first (works on Win11), fallback to HttpClient (works on Win10)
            try
            {
                await DownloadWithCurlAsync(packageInfo.Data, destFilePath, progress);
                return;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // curl.exe not found, fallback to HttpClient
            }

            // Fallback: standard HttpClient
            using var response = await _httpClient.GetAsync(packageInfo.Data, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var output = File.Create(destFilePath);
            await response.Content.CopyToStreamAsync(output, progress, cancellationToken);
        }

        private async Task DownloadWithCurlAsync(string url, string destFilePath, IProgress<double>? progress)
        {
            string authArgs = "";
            if (_httpClient.DefaultRequestHeaders.Authorization != null)
            {
                var auth = _httpClient.DefaultRequestHeaders.Authorization;
                authArgs = $"-H \"Authorization: {auth.Scheme} {auth.Parameter}\"";
            }

            var psi = new ProcessStartInfo
            {
                FileName = "curl.exe",
                Arguments = $"-s -L {authArgs} -o \"{destFilePath}\" \"{url}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start curl.exe for download");

            var error = await process.StandardError.ReadToEndAsync();
            await Task.Run(() => process.WaitForExit());

            if (process.ExitCode != 0)
                throw new System.Net.Http.HttpRequestException($"curl download failed ({process.ExitCode}): {error}");

            var fileInfo = new FileInfo(destFilePath);
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                var content = fileInfo.Exists ? File.ReadAllText(destFilePath) : "";
                if (content.Contains("403") || content.Contains("Forbidden"))
                {
                    File.Delete(destFilePath);
                    throw new System.Net.Http.HttpRequestException("Response status code does not indicate success: 403 (Forbidden).");
                }
                throw new System.Net.Http.HttpRequestException("Download failed - file is empty or missing");
            }

            progress?.Report(1.0);
        }
    }
}