using System;
using System.Collections.Generic;
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
        Task CreateZipWithProgress(string folderName, string zipName, string folderInsideZipName, IProgress<double> progress, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create silently zip files, can specify the list of files to do
        /// </summary>
        /// <param name="folderFenixName"></param>
        /// <param name="folderDataName"></param>
        /// <param name="zipName"></param>
        /// <param name="files"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task CreateZipWithoutProgress(string folderFenixName, string folderDataName, string zipName, List<string>? files = null, CancellationToken cancellationToken = default);
    }
}