using Onova.Updater.Internal;

namespace Onova.Updater
{
    // This executable applies the update by copying over new files.
    // It's required because updatee cannot update itself while the files are still in use.

    public static class Program
    {
        public static void Main(string[] args)
        {
            var updateeFilePath = args[0];
            var packageContentDirPath = args[1];
            var restartUpdatee = bool.Parse(args[2]);
            var routedArgs = args[3].FromBase64().GetString();
            var oldVersion = args.Length > 4 ? args[4] : "";
            var newVersion = args.Length > 5 ? args[5] : "";

            using var updater = new Updater(updateeFilePath, packageContentDirPath, restartUpdatee, routedArgs, oldVersion, newVersion);
            updater.Run();
        }
    }
}