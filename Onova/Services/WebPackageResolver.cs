using System;
using System.Collections.Generic;
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
            var response = await _httpClient.GetStringAsync(_manifestUrl, cancellationToken);
            

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

            // Download
            using var response = await _httpClient.GetAsync(packageInfo.Data, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            using var output = File.Create(destFilePath);
            await response.Content.CopyToStreamAsync(output, progress, cancellationToken);
        }
    }
}