using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Core
{
    public class PackageDependencyInfoComparer : IEqualityComparer<PackageDependencyInfo>
    {
        private readonly IPackageIdentityComparer _identityComparer;
        private readonly PackageDependencyComparer _dependencyComparer;

        public PackageDependencyInfoComparer()
            : this(PackageIdentityComparer.Default, PackageDependencyComparer.Default)
        {

        }

        public PackageDependencyInfoComparer(IPackageIdentityComparer identityComparer, PackageDependencyComparer dependencyComparer)
        {
            if (identityComparer == null)
            {
                throw new ArgumentNullException("identityComparer");
            }

            if (dependencyComparer == null)
            {
                throw new ArgumentNullException("dependencyComparer");
            }

            _identityComparer = identityComparer;
            _dependencyComparer = dependencyComparer;
        }

        /// <summary>
        /// Default comparer
        /// </summary>
        public static PackageDependencyInfoComparer Default
        {
            get
            {
                return new PackageDependencyInfoComparer();
            }
        }

        public bool Equals(PackageDependencyInfo x, PackageDependencyInfo y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
            {
                return false;
            }

            bool result = _identityComparer.Equals(x, y);

            if (result)
            {
                // counts must match
                result = x.Dependencies.Count() == y.Dependencies.Count();
            }

            if (result)
            {
                HashSet<PackageDependency> dependencies = new HashSet<PackageDependency>(_dependencyComparer);

                dependencies.UnionWith(x.Dependencies);

                int before = dependencies.Count;

                dependencies.UnionWith(y.Dependencies);

                // verify all dependencies are the same
                result = dependencies.Count == before;
            }

            return result;
        }

        public int GetHashCode(PackageDependencyInfo obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return 0;
            }

            HashCodeCombiner combiner = new HashCodeCombiner();

            combiner.AddObject(obj.Id);
            combiner.AddObject(obj.Version);

            // order the dependencies by hash code to make this consistent
            foreach (int hash in obj.Dependencies.Select(e => _dependencyComparer.GetHashCode(e)).OrderBy(h => h))
            {
                combiner.AddObject(hash);
            }

            return combiner.CombinedHash;
        }
    }
}
