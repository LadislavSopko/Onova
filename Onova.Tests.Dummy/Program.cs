using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Onova.Models;
using Onova.Services;

namespace Onova.Tests.Dummy
{
    class Backuper : IBackupper
    {
        public Task BackupAsync(string packageName, string version, string targetPath, CancellationToken cancellationToken)
        {
            // Do nothing
            return Task.CompletedTask;
        }

        public Task CreateZipWithProgress(string folderName, string zipName, IProgress<double> progress, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    class ProgreesFake : IMultiProgressBar
    {
        public void CreateGlobalProgressBar()
        {
            
        }

        public IProgress<double>? CreateProgressBar(string name, string description = "")
        {
            return new Progress<double>(p => Console.WriteLine($"{name}: {p:P0}"));
        }
    }

    // This executable is used as dummy for end-to-end testing.
    // It can print its current version and use Onova to update.

    public static class Program
    {
        private static Version Version => Assembly.GetExecutingAssembly().GetName().Version!;

        private static string AssemblyDirPath => AppDomain.CurrentDomain.BaseDirectory!;

        private static string LastRunFilePath => Path.Combine(AssemblyDirPath, $"lastrun-{Version}.txt");

        private static string PackagesDirPath => Path.Combine(AssemblyDirPath, "Packages");

        private static readonly IUpdateManager UpdateManager = new UpdateManager(
            new LocalPackageResolver(PackagesDirPath, "*.onv"),
            new ZipPackageExtractor(),
            new Backuper(),
            new AutomaticUpdateConfig()
            {
                Active = true
            });

        public static async Task Main(string[] args)
        {
            //var i = 0;
            //while(i == 0)
            //{
            //    Thread.Sleep(10);
            //}

            // Dump arguments to file.
            // This is only accurate enough for simple inputs.
            File.WriteAllLines(LastRunFilePath, args);

            // Get command name
            var command = args.FirstOrDefault();

            // Print current assembly version
            if (command == "version" || command == null)
            {
                Console.WriteLine(Version);
            }
            // Update to latest version
            else if (command == "update" || command == "update-and-restart")
            {
                var restart = command == "update-and-restart";
                var progressHandler = new ProgreesFake();

                await UpdateManager.CheckPerformUpdateAsync("c:\\3U\\OGSM", "c:\\3U\\OGSM\\data", restart, progressHandler);
            }
        }
    }
}