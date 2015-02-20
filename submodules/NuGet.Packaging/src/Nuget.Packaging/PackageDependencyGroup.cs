using NuGet.Frameworks;
using NuGet.Packaging.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// Package dependencies grouped to a target framework.
    /// </summary>
    public class PackageDependencyGroup
    {
        private readonly NuGetFramework _targetFramework;
        private readonly IEnumerable<PackageDependency> _packages;

        public PackageDependencyGroup(string targetFramework, IEnumerable<PackageDependency> packages)
        {
            if (packages == null)
            {
                throw new ArgumentNullException("packages");
            }

            if (String.IsNullOrEmpty(targetFramework))
            {
                _targetFramework = NuGetFramework.AnyFramework;
            }
            else
            {
                _targetFramework = NuGetFramework.Parse(targetFramework);
            }

            _packages = packages;
        }

        public PackageDependencyGroup(NuGetFramework targetFramework, IEnumerable<PackageDependency> packages)
        {
            if (targetFramework == null)
            {
                throw new ArgumentNullException("targetFramework");
            }

            if (packages == null)
            {
                throw new ArgumentNullException("packages");
            }

            _packages = packages;
        }

        /// <summary>
        /// Dependency group target framework
        /// </summary>
        public NuGetFramework TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        /// <summary>
        /// Package dependencies
        /// </summary>
        public IEnumerable<PackageDependency> Packages
        {
            get
            {
                return _packages;
            }
        }
    }
}
