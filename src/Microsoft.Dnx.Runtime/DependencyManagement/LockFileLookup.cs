using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class LockFileLookup
    {
        // REVIEW: Case sensitivity?
        private readonly Dictionary<Tuple<string, FrameworkName, string>, LockFileTargetLibrary> _lookup;
        private readonly Dictionary<Tuple<string, SemanticVersion>, LockFilePackageLibrary> _packages;

        public LockFileLookup(LockFile lockFile)
        {
            _lookup = new Dictionary<Tuple<string, FrameworkName, string>, LockFileTargetLibrary>();
            _packages = new Dictionary<Tuple<string, SemanticVersion>, LockFilePackageLibrary>();

            foreach (var t in lockFile.Targets)
            {
                foreach (var library in t.Libraries)
                {
                    // Each target has a single package version per id
                    _lookup[Tuple.Create(t.RuntimeIdentifier, t.TargetFramework, library.Name)] = library;
                }
            }

            foreach (var library in lockFile.PackageLibraries)
            {
                _packages[Tuple.Create(library.Name, library.Version)] = library;
            }
        }

        public LockFileTargetLibrary GetTargetLibrary(FrameworkName targetFramework, string packageId)
        {
            return GetTargetLibrary(runtimeId: null, targetFramework: targetFramework, packageId: packageId);
        }

        public LockFileTargetLibrary GetTargetLibrary(string runtimeId, FrameworkName targetFramework, string packageId)
        {
            LockFileTargetLibrary library;
            if (_lookup.TryGetValue(Tuple.Create(runtimeId, targetFramework, packageId), out library))
            {
                return library;
            }

            return null;
        }

        public LockFilePackageLibrary GetPackage(string id, SemanticVersion version)
        {
            LockFilePackageLibrary package;
            if (_packages.TryGetValue(Tuple.Create(id, version), out package))
            {
                return package;
            }

            return null;
        }

        public void Clear()
        {
            _lookup.Clear();
            _packages.Clear();
        }
    }
}
