using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Onova.Exceptions;
using Onova.Internal.Extensions;

namespace Onova.Services
{
    /// <summary>
    /// Resolves packages using multiple other package resolvers.
    /// </summary>
    public class AggregatePackageResolver : IPackageResolver
    {
        private readonly IReadOnlyList<IPackageResolver> _resolvers;

        /// <summary>
        /// Initializes an instance of <see cref="AggregatePackageResolver"/>.
        /// </summary>
        public AggregatePackageResolver(IReadOnlyList<IPackageResolver> resolvers)
        {
            _resolvers = resolvers;
        }

        /// <summary>
        /// Initializes an instance of <see cref="AggregatePackageResolver"/>.
        /// </summary>
        public AggregatePackageResolver(params IPackageResolver[] resolvers)
            : this((IReadOnlyList<IPackageResolver>) resolvers)
        {
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<VersionWithInfo>> GetPackageVersionsAsync(CancellationToken cancellationToken = default)
        {
            var aggregateVersions = new HashSet<Version>();
            List<VersionWithInfo> _ret = new List<VersionWithInfo>();

            // Get unique package versions provided by all resolvers
            foreach (var resolver in _resolvers)
            {
                var versions = await resolver.GetPackageVersionsAsync(cancellationToken);
                foreach(var v in versions)
                {
                    if (aggregateVersions.Add(v.Version))
                    {
                        _ret.Add(v);
                    }
                }
                aggregateVersions.AddRange(versions.Select(v => v.Version));
            }

            return _ret.ToArray();
        }

        private async Task<IPackageResolver?> TryGetResolverForPackageAsync(
            Version version,
            CancellationToken cancellationToken)
        {
            // Try to find the first resolver that has this package version
            foreach (var resolver in _resolvers)
            {
                var versions = await resolver.GetPackageVersionsAsync(cancellationToken);
                if (versions.Where(v => v.Version == version).FirstOrDefault() != default)
                    return resolver;
            }

            // Return null if none of the resolvers provide this package version
            return null;
        }

        /// <inheritdoc />
        public async Task DownloadPackageAsync(Version version, string destFilePath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            // Find a resolver that has this package version
            var resolver =
                await TryGetResolverForPackageAsync(version, cancellationToken) ??
                throw new PackageNotFoundException(version);

            // Download package
            await resolver.DownloadPackageAsync(version, destFilePath, progress, cancellationToken);
        }
    }
}