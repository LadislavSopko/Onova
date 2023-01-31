using Onova.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Onova.Models
{
    /// <summary>
    /// Extends base with note
    /// </summary>
    public class CheckForUpdatesResultWithNote : CheckForUpdatesResult
    {
        internal CheckForUpdatesResultWithNote(IReadOnlyList<VersionWithInfo> versns, Version? lastVersion, bool canUpdate) : base(versns.Select(v => v.Version).ToArray(), lastVersion, canUpdate)
        {
            VersionsWithInfo = versns;
        }

        /// <summary>
        /// All available package versions.
        /// </summary>
        public IReadOnlyList<VersionWithInfo> VersionsWithInfo { get; protected set; }


        /// <summary>
        /// Generate Result from available update
        /// </summary>
        /// <param name="versions"></param>
        /// <param name="lastVersion"></param>
        /// <param name="doUpdate"></param>
        /// <returns></returns>
        public static CheckForUpdatesResult OkWithNote(IReadOnlyList<VersionWithInfo> versions, Version? lastVersion, bool doUpdate)
        {
            return new CheckForUpdatesResultWithNote(versions, lastVersion, doUpdate);
        }
    }

    /// <summary>
    /// Result of checking for updates.
    /// </summary>
    public class CheckForUpdatesResult
    {
        /// <summary>
        /// All available package versions.
        /// </summary>
        public IReadOnlyList<Version> Versions { get; }

        /// <summary>
        /// Last available package version.
        /// Null if there are no available packages.
        /// </summary>
        public Version? LastVersion { get; }

        /// <summary>
        /// Whether there is a package with higher version than the current version.
        /// </summary>
        public bool CanUpdate { get; }

        /// <summary>
        /// Eventual error
        /// </summary>
        public Exception? Error { get; internal set; }

        /// <summary>
        /// Initializes a new instance of <see cref="CheckForUpdatesResult"/>.
        /// </summary>
        internal CheckForUpdatesResult(IReadOnlyList<Version> versions, Version? lastVersion, bool canUpdate)
        {
            Versions = versions;
            LastVersion = lastVersion;
            CanUpdate = canUpdate;
            Error = default;
        }

        /// <summary>
        /// Generate Result from available update
        /// </summary>
        /// <param name="versions"></param>
        /// <param name="lastVersion"></param>
        /// <param name="doUpdate"></param>
        /// <returns></returns>
        public static CheckForUpdatesResult Ok(IReadOnlyList<Version> versions, Version? lastVersion, bool doUpdate)
        {
            return new CheckForUpdatesResult(versions, lastVersion, doUpdate);
        }

        /// <summary>
        /// Generate Result from exception
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static CheckForUpdatesResult FromError(Exception ex)
        {
            return new CheckForUpdatesResult(new List<Version>(), default, false)
            {
                Error = ex
            };
        }

        /// <summary>
        /// No update
        /// </summary>
        /// <returns></returns>
        public static CheckForUpdatesResult NoUpdate()
        {
            return new CheckForUpdatesResult(new List<Version>(), default, false);
        }
    }
}