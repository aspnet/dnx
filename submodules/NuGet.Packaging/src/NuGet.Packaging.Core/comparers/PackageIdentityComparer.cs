using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// Compares the Id, Version, and Version release label. Version build metadata is ignored.
    /// </summary>
    public class PackageIdentityComparer : IPackageIdentityComparer
    {
        private readonly IVersionComparer _versionComparer;

        /// <summary>
        /// Default version range comparer.
        /// </summary>
        public PackageIdentityComparer()
            : this(new VersionComparer(VersionComparison.Default))
        {

        }

        /// <summary>
        /// Compare versions with a specific VersionComparison
        /// </summary>
        public PackageIdentityComparer(VersionComparison versionComparison)
            : this(new VersionComparer(versionComparison))
        {

        }

        /// <summary>
        /// Compare versions with a specific IVersionComparer
        /// </summary>
        public PackageIdentityComparer(IVersionComparer versionComparer)
        {
            if (versionComparer == null)
            {
                throw new ArgumentNullException("versionComparer");
            }

            _versionComparer = versionComparer;
        }

        /// <summary>
        /// Default comparer that compares on the id, version, and version release labels.
        /// </summary>
        public static IPackageIdentityComparer Default
        {
            get
            {
                return new PackageIdentityComparer();
            }
        }

        /// <summary>
        /// True if the package identities are the same when ignoring build metadata.
        /// </summary>
        public bool Equals(PackageIdentity x, PackageIdentity y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(x.Id, y.Id)
                && _versionComparer.Equals(x.Version, y.Version);
        }

        /// <summary>
        /// Hash code of the id and version
        /// </summary>
        public int GetHashCode(PackageIdentity obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return 0;
            }

            HashCodeCombiner combiner = new HashCodeCombiner();

            combiner.AddObject(obj.Id.ToUpperInvariant());
            combiner.AddObject(_versionComparer.GetHashCode(obj.Version));

            return combiner.CombinedHash;
        }

        /// <summary>
        /// Compares on the Id first, then version
        /// </summary>
        public int Compare(PackageIdentity x, PackageIdentity y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return 0;
            }

            if (Object.ReferenceEquals(x, null))
            {
                return -1;
            }

            if (Object.ReferenceEquals(y, null))
            {
                return 1;
            }

            int result = StringComparer.OrdinalIgnoreCase.Compare(x.Id, y.Id);

            if (result != 0)
            {
                result = _versionComparer.Compare(x.Version, y.Version);
            }

            return result;
        }
    }
}
