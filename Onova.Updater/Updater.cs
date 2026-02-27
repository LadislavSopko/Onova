using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using Onova.Updater.Internal;

namespace Onova.Updater
{
    public class Updater : IDisposable
    {
        private readonly string _updateeFilePath;
        private readonly string _packageContentDirPath;
        private readonly bool _restartUpdatee;
        private readonly string _routedArgs;
        private readonly string _oldVersion;
        private readonly string _newVersion;

        public static readonly TextWriter _log = File.CreateText(
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log.txt")
        );

        public Updater(
            string updateeFilePath,
            string packageContentDirPath,
            bool restartUpdatee,
            string routedArgs,
            string oldVersion,
            string newVersion)
        {
            _updateeFilePath = updateeFilePath;
            _packageContentDirPath = packageContentDirPath;
            _restartUpdatee = restartUpdatee;
            _routedArgs = routedArgs;
            _oldVersion = oldVersion;
            _newVersion = newVersion;
        }

        public static void WriteLog(string content)
        {
            var date = DateTimeOffset.Now;
            _log.WriteLine($"{date:dd-MMM-yyyy HH:mm:ss.fff}> {content}");
            _log.Flush();
        }

        private void RunCore()
        {
            var updateeDirPath = Path.GetDirectoryName(_updateeFilePath);

            if (updateeDirPath == default)
            {
                throw new ApplicationException("Missing updateeDirPath");
            }

            // Wait until updatee is writable to ensure all running instances have exited
            WriteLog("Waiting for all running updatee instances to exit...");
            while (!FileEx.CheckWriteAccess(_updateeFilePath))
                Thread.Sleep(100);

            // Copy over the package contents
            WriteLog("Copying package contents from storage to updatee's directory...");
            DirectoryEx.Copy(_packageContentDirPath, updateeDirPath);

            if (!string.IsNullOrEmpty(_oldVersion))
            {
                var quarantine = Path.Combine(updateeDirPath, "DLLS");
                Directory.CreateDirectory(quarantine);
                foreach (var dll in Directory.GetFiles(updateeDirPath, "*.dll"))
                {
                    var v = FileVersionInfo.GetVersionInfo(dll).FileVersion;
                    if (v == _oldVersion)
                    {
                        WriteLog($"Removing old DLL: {dll}");
                        var dest = Path.Combine(quarantine, Path.GetFileName(dll));
                        if (File.Exists(dest)) File.Delete(dest);
                        File.Move(dll, dest);
                        var pdb = Path.ChangeExtension(dll, ".pdb");
                        if (File.Exists(pdb))
                        {
                            var pdbDest = Path.Combine(quarantine, Path.GetFileName(pdb));
                            if (File.Exists(pdbDest)) File.Delete(pdbDest);
                            File.Move(pdb, pdbDest);
                        }
                    }
                }
            }

            // Restart updatee if requested
            if (_restartUpdatee)
            {
                var startInfo = new ProcessStartInfo
                {
                    WorkingDirectory = updateeDirPath,
                    Arguments = _routedArgs,
                    UseShellExecute = true // avoid sharing console window with updatee
                                           //UseShellExecute = false, // we need continue on the same console as Updatee (it is console app)
                                           //RedirectStandardError  = false,
                                           //RedirectStandardInput  = false,
                                           //RedirectStandardOutput = false
                };

                // If updatee is an .exe file - start it directly
                if (string.Equals(Path.GetExtension(_updateeFilePath), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    startInfo.FileName = _updateeFilePath;
                }
                // If not - figure out what to do with it
                else
                {
                    // If there's an .exe file with same name - start it instead
                    // Security vulnerability?
                    if (File.Exists(Path.ChangeExtension(_updateeFilePath, ".exe")))
                    {
                        startInfo.FileName = Path.ChangeExtension(_updateeFilePath, ".exe");
                    }
                    // Otherwise - start the updatee using dotnet SDK
                    else
                    {
                        startInfo.FileName = "dotnet";
                        startInfo.Arguments = $"{_updateeFilePath} {_routedArgs}";
                    }
                }

                WriteLog($"Restarting updatee [{startInfo.FileName} {startInfo.Arguments}]...");

                using var restartedUpdateeProcess = Process.Start(startInfo);
                WriteLog($"Restarted as pid:{restartedUpdateeProcess?.Id}.");
            }

            // Delete package content directory
            WriteLog("Deleting package contents from storage...");
            Directory.Delete(_packageContentDirPath, true);

            // run post update commands

            //var _ThisProcess = Process.GetCurrentProcess(); // Or whatever method you are using
            //var _processPath = _ThisProcess.MainModule?.FileName;
            //var fileName = Path.GetFileName(_processPath);

            var newPath = updateeDirPath; // Path.GetDirectoryName(fileName);

            Process process = new Process();

            var fileName = Path.Combine(newPath, "postUpdate.bat");
            if (File.Exists(fileName))
            {

                process.StartInfo.WorkingDirectory = newPath;
                process.StartInfo.FileName = fileName;
                process.StartInfo.Arguments = _oldVersion;
                process.Start();
                process.WaitForExit();
                process.Close();

            }
        }

        public void Run()
        {
            var updaterVersion = Assembly.GetExecutingAssembly().GetName().Version;

            WriteLog(
                $"Onova Updater v{updaterVersion} started with the following arguments:" + Environment.NewLine +
                $"  UpdateeFilePath = {_updateeFilePath}" + Environment.NewLine +
                $"  PackageContentDirPath = {_packageContentDirPath}" + Environment.NewLine +
                $"  RestartUpdatee = {_restartUpdatee}" + Environment.NewLine +
                $"  RoutedArgs = {_routedArgs}" +
                $"  OldVersion = {_oldVersion}"
            );

            try
            {
                using Process processStop = Process.Start("net", "stop \"Fenix Manager\"");
                processStop.WaitForExit();

                RunCore();

                Version v = new(_newVersion);
                if (v.Build >= 100)
                {
                    using Process processStart = Process.Start("net", "start \"Fenix Manager\"");
                    processStart.WaitForExit();
                }
            }
            catch (Exception ex)
            {
                WriteLog(ex.ToString());
            }
        }

        public void Dispose() => _log.Dispose();
    }
}