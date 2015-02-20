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
    /// Represents a package dependency Id and allowed version range.
    /// </summary>
    public class PackageDependency : IEquatable<PackageDependency>
    {
        private readonly string _id;
        private readonly VersionRange _versionRange;

        public PackageDependency(string id)
            : this (id, VersionRange.All)
        {

        }

        public PackageDependency(string id, VersionRange versionRange)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id");
            }

            _id = id;
            _versionRange = versionRange ?? VersionRange.All;
        }

        /// <summary>
        /// Dependency package Id
        /// </summary>
        public string Id
        {
            get
            {
                return _id;
            }
        }

        /// <summary>
        /// Range of versions allowed for the depenency
        /// </summary>
        public VersionRange VersionRange
        {
            get
            {
                return _versionRange;
            }
        }

        public bool Equals(PackageDependency other)
        {
            return PackageDependencyComparer.Default.Equals(this, other);
        }

        public override bool Equals(object obj)
        {
            PackageDependency dependency = obj as PackageDependency;

            if (dependency != null)
            {
                return Equals(dependency);
            }

            return false;
        }

        /// <summary>
        /// Hash code from the default PackageDependencyComparer
        /// </summary>
        public override int GetHashCode()
        {
            return PackageDependencyComparer.Default.GetHashCode(this);
        }

        /// <summary>
        /// Id and Version range string
        /// </summary>
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} {1}", Id, VersionRange.ToNormalizedString());
        }
    }
}
