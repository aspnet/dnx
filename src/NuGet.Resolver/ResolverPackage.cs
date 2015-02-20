using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    public class ResolverPackage : PackageDependencyInfo
    {
        public bool Absent { get; set; }

        public ResolverPackage(string id)
            : this(id, null)
        {

        }

        public ResolverPackage(string id, NuGetVersion version)
            : this(id, version, Enumerable.Empty<PackageDependency>())
        {

        }

        public ResolverPackage(string id, NuGetVersion version, IEnumerable<PackageDependency> dependencies, bool absent=false)
            : base(id, version, dependencies)
        {
            Absent = absent;
        }

        public ResolverPackage(PackageDependencyInfo info, bool absent)
            : this(info.Id, info.Version, info.Dependencies, absent)
        {

        }

        /// <summary>
        /// A package identity and its dependencies.
        /// </summary>
        /// <param name="identity"></param>
        /// <param name="dependencies">Dependencies from the relevant target framework group. This group should be selected based on the 
        /// project target framework.</param>
        public ResolverPackage(PackageIdentity identity, IEnumerable<PackageDependency> dependencies)
            : this(identity.Id, identity.Version, dependencies)
        {

        }

        /// <summary>
        /// Find the version range for the given package. The package may not exist.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public VersionRange FindDependencyRange(string id)
        {
            var dependency = Dependencies.Where(e => StringComparer.OrdinalIgnoreCase.Equals(id, e.Id)).SingleOrDefault();
            if (dependency == null)
            {
                return null;
            }

            if (dependency.VersionRange == null)
            {
                return VersionRange.Parse("0.0"); //Any version allowed
            }

            return dependency.VersionRange;
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} {1}", Id, Version);
        }
    }
}
