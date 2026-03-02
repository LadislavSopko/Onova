using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Onova.Exceptions;
using Onova.Internal;
using Onova.Internal.Extensions;
using Onova.Models;
using Onova.Services;

namespace Onova
{



    /// <summary>
    /// Entry point for handling application updates.
    /// </summary>
    public class UpdateManager : IUpdateManager
    {
        private const string UpdaterResourceName = "Onova.Updater.exe";

        private readonly IPackageResolver _resolver;
        private readonly IPackageExtractor _extractor;
        private readonly IBackupper _backupper;

        private readonly string _storageDirPath;
        private readonly string _updaterFilePath;
        private readonly string _lockFilePath;

        private LockFile? _lockFile;
        private bool _isDisposed;

        /// <inheritdoc />
        public AssemblyMetadata Updatee { get; }

        private AutomaticUpdateConfig _config;

        /// <summary>
        /// Initializes an instance of <see cref="UpdateManager"/>.
        /// </summary>
        public UpdateManager(AssemblyMetadata updatee, IPackageResolver resolver, IPackageExtractor extractor, IBackupper backupper, AutomaticUpdateConfig cfg)
        {

            _config = cfg ?? new AutomaticUpdateConfig();

            Platform.EnsureWindows();

            Updatee = updatee;
            _resolver = resolver;
            _extractor = extractor;
            _backupper = backupper;

            // Set storage directory path
            _storageDirPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Onova",
                updatee.Name
            );

            // Set updater executable file path
            _updaterFilePath = Path.Combine(_storageDirPath, $"{updatee.Name}.Updater.exe");

            // Set lock file path
            _lockFilePath = Path.Combine(_storageDirPath, "Onova.lock");
        }

        /// <summary>
        /// Initializes an instance of <see cref="UpdateManager"/> on the entry assembly.
        /// </summary>
        public UpdateManager(IPackageResolver resolver, IPackageExtractor extractor, IBackupper backupper, AutomaticUpdateConfig cfg)
            : this(AssemblyMetadata.FromEntryAssembly(), resolver, extractor, backupper, cfg)
        {
        }

        private string GetPackageFilePath(Version version) => Path.Combine(_storageDirPath, $"{version}.onv");

        private string GetPackageContentDirPath(Version version) => Path.Combine(_storageDirPath, $"{version}");

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        private void EnsureLockFileAcquired()
        {
            // Ensure storage directory exists
            Directory.CreateDirectory(_storageDirPath);

            // Try to acquire lock file if it's not acquired yet
            _lockFile ??= LockFile.TryAcquire(_lockFilePath);

            // If failed to acquire - throw
            if (_lockFile == null)
                throw new LockFileNotAcquiredException();
        }

        private void EnsureUpdaterNotLaunched()
        {
            // Check whether we have write access to updater executable
            // (this is a reasonably accurate check for whether that process is running)
            if (File.Exists(_updaterFilePath) && !FileEx.CheckWriteAccess(_updaterFilePath))
                throw new UpdaterAlreadyLaunchedException();
        }

        private void EnsureUpdatePrepared(Version version)
        {
            if (!IsUpdatePrepared(version))
                throw new UpdateNotPreparedException(version);
        }

        /// <inheritdoc />
        public async Task<CheckForUpdatesResult> CheckForUpdatesAsync(Func<VersionWithInfo, bool>? filter = null, CancellationToken cancellationToken = default)
        {
            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();

            if (_config.Active)
            {
                try
                {
                    var flt = filter != null ? filter : (e) => true;

                    var max_updatable = Version.Parse(_config.MaxUpdatableVersion);

                    // Get versions
                    var versions = (await _resolver.GetPackageVersionsAsync(cancellationToken)).Where(flt).ToList();
                    var lastVersion = versions.Where(v => v.Version <= max_updatable).Select(v => v.Version).Max();
                    var canUpdate = lastVersion != null; // && Updatee.Version < lastVersion; (show all possible) we need also downgrade

                    return canUpdate ? CheckForUpdatesResultWithNote.OkWithNote(versions, lastVersion, canUpdate) :
                        CheckForUpdatesResult.NoUpdate();

                }
                catch (Exception ex)
                {
                    return CheckForUpdatesResult.FromError(ex);
                }
            }
            else
            {
                return CheckForUpdatesResult.NoUpdate();
            }

        }

        /// <inheritdoc />
        public bool IsUpdatePrepared(Version version)
        {
            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();

            // Get package file path and content directory path
            var packageFilePath = GetPackageFilePath(version);
            var packageContentDirPath = GetPackageContentDirPath(version);

            // Package content directory should exist
            // Updater file should exist
            return !File.Exists(packageFilePath) &&
                   Directory.Exists(packageContentDirPath) &&
                   File.Exists(_updaterFilePath);
        }

        /// <inheritdoc />
        public IReadOnlyList<Version> GetPreparedUpdates()
        {
            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();

            var result = new List<Version>();

            // Enumerate all immediate directories in storage
            if (Directory.Exists(_storageDirPath))
            {
                foreach (var packageContentDirPath in Directory.EnumerateDirectories(_storageDirPath))
                {
                    // Get directory name
                    var packageContentDirName = Path.GetFileName(packageContentDirPath);

                    // Try to extract version out of the name
                    if (string.IsNullOrWhiteSpace(packageContentDirName) ||
                        !Version.TryParse(packageContentDirName, out var version))
                    {
                        continue;
                    }

                    // If this package is prepared - add it to the list
                    if (IsUpdatePrepared(version))
                    {
                        result.Add(version);
                    }
                }
            }

            return result;
        }

        /// <inheritdoc />
        public async Task PrepareUpdateAsync(Version version, Version? backupVersion = null, string basePath = "C:\\3U\\OGSM", string persistorPath = "c:\\3U\\OGSM\\data",
            IMultiProgressBar multiProgress = null, bool doBackup = true, bool doDownload = true, List<string>? silentFiles = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(basePath) || string.IsNullOrEmpty(persistorPath))
            {
                doBackup = false;
            }

            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();
            EnsureLockFileAcquired();
            EnsureUpdaterNotLaunched();

            // Get package file path and content directory path
            var packageFilePath = GetPackageFilePath(version);
            var packageContentDirPath = GetPackageContentDirPath(version);

            // Ensure storage directory exists
            Directory.CreateDirectory(_storageDirPath);

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
            {
                ZipPackageBackupper.StartServiceWithSC("MongoDBFenix");
            };

            // Create backup-specific progress bars if needed
            IProgress<double>? binProgress = null;
            IProgress<double>? dataProgress = null;

            if (doBackup)
            {
                binProgress = multiProgress.CreateProgressBar("Bin Backup", "Compressing bin folder...");
                dataProgress = multiProgress.CreateProgressBar("Data Backup", "Compressing data folder...");
            }

            // Create download-specific progress bars if needed
            IProgress<double>? downloadProgress = null;
            IProgress<double>? extractingProgress = null;

            if (doDownload)
            {
                downloadProgress = multiProgress.CreateProgressBar("Download", "Downloading package...");
                extractingProgress = multiProgress.CreateProgressBar("Extracting", "Extracting package...");
            }

            multiProgress.CreateGlobalProgressBar();

            // Create task list
            var tasks = new List<Task>();

            string autoBackupFolderName = $"Versions Backups\\bk_{SanitizeFileName(backupVersion.ToString())}";
            var backupFolder = Path.Combine(basePath, autoBackupFolderName);
            Directory.CreateDirectory(backupFolder);

            // Add backup tasks if needed
            if (doBackup)
            {
                string binZipPath = Path.Combine(backupFolder, SanitizeFileName($"Fenix_Core_{DateTime.Now:G}.zip"));
                string dataZipPath = Path.Combine(backupFolder, SanitizeFileName($"data_{DateTime.Now:G}.zip"));

                var backupBinTask = Task.Run(async () =>
                {
                    try
                    {
                        await _backupper.CreateZipWithProgress(AppDomain.CurrentDomain.BaseDirectory, binZipPath, "Fenix_Core", binProgress, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\nBackup bin failed: {ex.Message}");
                        if (File.Exists(binZipPath))
                        {
                            try { File.Delete(binZipPath); } catch { }
                        }
                    }
                });

                var backupDataTask = Task.Run(async () =>
                {
                    try
                    {
                        if (ZipPackageBackupper.StopServiceWithSC("MongoDBFenix"))
                        {
                            await _backupper.CreateZipWithProgress(persistorPath, dataZipPath, "data", dataProgress, cancellationToken);
                        }
                        ZipPackageBackupper.StartServiceWithSC("MongoDBFenix");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\nBackup data failed: {ex.Message}");
                        if (File.Exists(dataZipPath))
                        {
                            try { File.Delete(dataZipPath); } catch { }
                        }
                    }
                });

                tasks.Add(backupBinTask);
                tasks.Add(backupDataTask);
            }

            if (doDownload)
            {
                var downloadTask = Task.Run(async () =>
                {
                    try
                    {
                        await _resolver.DownloadPackageAsync(version, packageFilePath, downloadProgress, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\nDownload failed: {ex.Message}");
                        throw;
                    }
                });

                tasks.Add(downloadTask);
            }


            string silentZipPath = Path.Combine(backupFolder, SanitizeFileName($"Essential_{DateTime.Now:G}.zip"));

            var silentBackupTask = Task.Run(async () =>
            {
                try
                {
                    await _backupper.CreateZipWithoutProgress(AppDomain.CurrentDomain.BaseDirectory, persistorPath, silentZipPath, silentFiles, cancellationToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nSilent backup failed: {ex.Message}");
                    if (File.Exists(silentZipPath))
                    {
                        try { File.Delete(silentZipPath); } catch { }
                    }
                }
            });

            tasks.Add(silentBackupTask);

            if (string.IsNullOrEmpty(basePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n'base_path' is empty in config, skipping backup!");
                Console.ForegroundColor = ConsoleColor.White;
            }

            if (string.IsNullOrEmpty(persistorPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n'persistor_base_path' is empty in config, skipping backup!");
                Console.ForegroundColor = ConsoleColor.White;
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            if (doDownload)
            {
                DirectoryEx.Reset(packageContentDirPath);
                await _extractor.ExtractPackageAsync(packageFilePath, packageContentDirPath, extractingProgress, cancellationToken);

                if (File.Exists(packageFilePath))
                    File.Delete(packageFilePath);

                await Assembly.GetExecutingAssembly().ExtractManifestResourceAsync(UpdaterResourceName, _updaterFilePath);
            }
        }

        /// <inheritdoc />
        public void LaunchUpdater(Version version, bool restart, string restartArguments, string oldVersion = "", string newVersion = "")
        {
            // Ensure that the current state is valid for this operation
            EnsureNotDisposed();
            EnsureLockFileAcquired();
            EnsureUpdaterNotLaunched();
            EnsureUpdatePrepared(version);

            // Get package content directory path
            var packageContentDirPath = GetPackageContentDirPath(version);

            // Get original command line arguments and encode them to avoid issues with quotes
            var routedArgs = restartArguments.GetBytes().ToBase64();

            // Prepare arguments
            var updaterArgs = $"\"{Updatee.FilePath}\" \"{packageContentDirPath}\" \"{restart}\" \"{routedArgs}\" \"{oldVersion}\" \"{newVersion}\"";

            // Decide if updater needs to be elevated
            var updateeDirPath = Path.GetDirectoryName(Updatee.FilePath);

            var updaterNeedsElevation =
                !string.IsNullOrWhiteSpace(updateeDirPath) &&
                !DirectoryEx.CheckWriteAccess(updateeDirPath);

            // Create updater process start info
            var updaterStartInfo = new ProcessStartInfo
            {
                FileName = _updaterFilePath,
                Arguments = updaterArgs,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            // If updater needs to be elevated - use shell execute with "runas"
            if (updaterNeedsElevation)
            {
                updaterStartInfo.Verb = "runas";
                updaterStartInfo.UseShellExecute = true;
            }

            // Create and start updater process
            var updaterProcess = new Process { StartInfo = updaterStartInfo };
            using (updaterProcess)
                updaterProcess.Start();
        }

        private static string SanitizeFileName(string input)
        {
            // Replace invalid filename characters with underscore
            string invalidChars = new string(Path.GetInvalidFileNameChars());
            string invalidRegex = $"[{Regex.Escape(invalidChars)}]";
            return Regex.Replace(input, invalidRegex, "_");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (!_isDisposed)
            {
                _isDisposed = true;
                _lockFile?.Dispose();
            }
        }


    }
}