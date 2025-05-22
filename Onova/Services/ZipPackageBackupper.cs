using Onova.Internal.Extensions;
using Onova.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Onova.Services
{
    /// <summary>
    /// Compress files into zip-archived packages.
    /// </summary>
    public class ZipPackageBackupper : IBackupper
    {
        private readonly string _basePath = "C:\\3U\\OGSM";

        /// <inheritdoc/>

        public async Task CreateZipWithProgress(string folderName, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var sourceDirPath = Path.Combine(_basePath, folderName);
            var destFilePath = Path.Combine(_basePath, SanitizeFileName($"{folderName}_{DateTime.Now:G}.zip"));

            try
            {
                if (Directory.Exists(sourceDirPath))
                {
                    // Get all files to compress
                    var files = Directory.GetFiles(sourceDirPath, "*", SearchOption.AllDirectories);

                    // For progress reporting
                    var totalBytes = files.Sum(f => new FileInfo(f).Length);
                    var totalBytesCopied = 0L;

                    // Create the zip file
                    using var archive = ZipFile.Open(destFilePath, ZipArchiveMode.Create);

                    // Loop through all files
                    foreach (var file in files)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        // Check space every few files
                        if (totalBytesCopied % 10 == 0) // Check every 10 files
                        {
                            var drive = new DriveInfo(Path.GetPathRoot(destFilePath));
                            if (drive.AvailableFreeSpace < 500L * 1024 * 1024) // Less than 500MB left
                            {
                                throw new Exception("No more space available for backups");
                            }
                        }

                        // Get the relative path for the entry name
                        var entryName = file.Substring(sourceDirPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                        // Create entry in the archive
                        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                        // Write file content to the entry
                        using var input = File.OpenRead(file);
                        using var output = entry.Open();

                        using var buffer = PooledBuffer.ForStream();
                        int bytesCopied;
                        do
                        {
                            bytesCopied = await input.CopyBufferedToAsync(output, buffer.Array, cancellationToken);
                            totalBytesCopied += bytesCopied;
                            progress?.Report(1.0 * totalBytesCopied / totalBytes);
                        } while (bytesCopied > 0);
                    }
                }
            }
            finally
            {
                if (folderName == "data")
                {
                    StartServiceWithSC("MongoDBFenix");
                }
            }
        }

        private static string SanitizeFileName(string input)
        {
            // Replace invalid filename characters with underscore
            string invalidChars = new string(Path.GetInvalidFileNameChars());
            string invalidRegex = $"[{Regex.Escape(invalidChars)}]";
            return Regex.Replace(input, invalidRegex, "_");
        }


        public static bool StartServiceWithSC(string serviceName)
        {
            try
            {
                Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"start \"{serviceName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process.WaitForExit();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool StopServiceWithSC(string serviceName)
        {
            try
            {
                Process process = Process.Start(new ProcessStartInfo
                {
                    FileName = "sc.exe",
                    Arguments = $"stop \"{serviceName}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                process.WaitForExit();
                return process.ExitCode == 0 || process.ExitCode == 1062; //Mongo stopped or already stop when called this
            }
            catch
            {
                return false;
            }
        }

    }
}