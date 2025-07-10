using System;
using System.Threading;
using System.Threading.Tasks;

namespace Onova.Services
{
    /// <summary>
    /// Provider for compressing packages.
    /// </summary>
    public interface IBackupper
    {
        /// <summary>
        /// Compress contents of the given directory to the given output package.
        /// </summary>
        Task CreateZipWithProgress(string folderName, string zipName, IProgress<double> progress, CancellationToken cancellationToken = default);
    }
}