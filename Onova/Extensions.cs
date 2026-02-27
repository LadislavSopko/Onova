using System;
using System.Threading;
using System.Threading.Tasks;
using Onova.Internal;
using Onova.Services;

namespace Onova
{
    /// <summary>
    /// Extensions for <see cref="Onova"/>.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Launches an external executable that will apply an update to given version, once this application exits.
        /// The updater can be instructed to also restart the application after it's updated.
        /// If the application is to be restarted, it will receive the same command line arguments as it did initially.
        /// </summary>
        public static void LaunchUpdater(this IUpdateManager manager, Version version, bool restart = true, string oldVersion = "", string newVersion = "") =>
            manager.LaunchUpdater(version, restart, EnvironmentEx.GetCommandLineWithoutExecutable(), oldVersion, newVersion);

        /// <summary>
        /// Checks for new version and performs an update if available.
        /// </summary>
        public static async Task CheckPerformUpdateAsync(this IUpdateManager manager, string basePath, string persistorBasePath, bool restart = true,
            IMultiProgressBar? multiProgress = null, CancellationToken cancellationToken = default)
        {
            // Check
            var result = await manager.CheckForUpdatesAsync(null, cancellationToken);
            if (result.CanUpdate && result.LastVersion != null)
            {
                // Prepare
                await manager.PrepareUpdateAsync(result.LastVersion, result.LastVersion, basePath, persistorBasePath, multiProgress, cancellationToken: cancellationToken);

                // Apply
                manager.LaunchUpdater(result.LastVersion, restart);

                // Exit
                Environment.Exit(0);
            }
        }
    }
}