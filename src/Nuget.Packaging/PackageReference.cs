using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// Represents a package element in the packages.config file
    /// </summary>
    public class PackageReference
    {
        private readonly PackageIdentity _identity;
        private readonly VersionRange _allowedVersions;
        private readonly NuGetFramework _targetFramework;
        private readonly bool _developmentDependency;
        private readonly bool _requireReinstallation;
        private readonly bool _userInstalled;

        /// <summary>
        /// Creates a new packages config entry
        /// </summary>
        public PackageReference(PackageIdentity identity, NuGetFramework targetFramework)
            : this(identity, targetFramework, true)
        {

        }

        /// <summary>
        /// Creates a new packages config entry
        /// </summary>
        public PackageReference(PackageIdentity identity, NuGetFramework targetFramework, bool userInstalled)
            : this(identity, targetFramework, userInstalled, false, false)
        {

        }

        /// <summary>
        /// Creates a new packages config entry
        /// </summary>
        public PackageReference(PackageIdentity identity, NuGetFramework targetFramework, bool userInstalled, bool developmentDependency, bool requireReinstallation)
            : this(identity, targetFramework, userInstalled, developmentDependency, requireReinstallation, null)
        {

        }

        /// <summary>
        /// Creates a new packages config entry
        /// </summary>
        /// <param name="identity">Package id and version</param>
        /// <param name="targetFramework">Package target framework installed to the project</param>
        /// <param name="userInstalled">True if the user installed this package directly</param>
        /// <param name="developmentDependency">True if the package is a development dependency</param>
        /// <param name="requireReinstallation">True if this package needs to be reinstalled</param>
        /// <param name="allowedVersions">Restrict package versions to the allowedVersions range</param>
        public PackageReference(PackageIdentity identity, NuGetFramework targetFramework, bool userInstalled, bool developmentDependency, bool requireReinstallation, VersionRange allowedVersions)
        {
            _identity = identity;
            _allowedVersions = allowedVersions;
            _targetFramework = targetFramework;
            _developmentDependency = developmentDependency;
            _userInstalled = userInstalled;
            _requireReinstallation = requireReinstallation;
        }

        /// <summary>
        /// Id and Version of the package
        /// </summary>
        public PackageIdentity PackageIdentity
        {
            get
            {
                return _identity;
            }
        }

        /// <summary>
        /// The allowed range of versions that this package can be upgraded/downgraded to.
        /// </summary>
        /// <remarks>This is null if unbounded</remarks>
        public VersionRange AllowedVersions
        {
            get
            {
                return _allowedVersions;
            }
        }

        /// <summary>
        /// True if allowedVersions exists.
        /// </summary>
        public bool HasAllowedVersions
        {
            get
            {
                return _allowedVersions != null;
            }
        }

        /// <summary>
        /// Installed target framework version of the package.
        /// </summary>
        public NuGetFramework TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        /// <summary>
        /// Development dependency
        /// </summary>
        public bool IsDevelopmentDependency
        {
            get
            {
                return _developmentDependency;
            }
        }

        /// <summary>
        /// True if the user installed or updated this package directly.
        /// False if this package was installed as a dependency by another package.
        /// </summary>
        public bool IsUserInstalled
        {
            get
            {
                return _userInstalled;
            }
        }

        /// <summary>
        /// Require reinstallation
        /// </summary>
        public bool RequireReinstallation
        {
            get
            {
                return _requireReinstallation;
            }
        }

        /// <summary>
        /// Displays the identity and target framework of the reference.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} {1}", PackageIdentity.ToString(), TargetFramework.ToString());
        }
    }
}
