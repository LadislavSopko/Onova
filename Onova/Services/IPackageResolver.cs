using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Onova.Services
{
    /// <summary>
    /// Version info with small note
    /// </summary>
    public class VersionWithInfo
    {
        /// <summary>
        /// Creates Version Info
        /// </summary>
        /// <param name="version"></param>
        /// <param name="data"></param>
        /// <param name="note"></param>
        public VersionWithInfo(Version version, string data, string note)
        {
            Version = version;
            Note = note;
            Data = data;
        }

        /// <summary>
        /// Vresion
        /// </summary>
        public Version Version { get; protected set; }

        /// <summary>
        /// Note
        /// </summary>
        public string Note { get; protected set; }

        /// <summary>
        /// Arbitrary data
        /// </summary>
        public string Data { get; protected set; }

    }

    /// <summary>
    /// Provider for resolving packages.
    /// </summary>
    public interface IPackageResolver
    {
        /// <summary>
        /// Gets all available package versions.
        /// </summary>
        Task<IReadOnlyList<VersionWithInfo>> GetPackageVersionsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads given package version.
        /// </summary>
        Task DownloadPackageAsync(Version version, string destFilePath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default);
    }
}