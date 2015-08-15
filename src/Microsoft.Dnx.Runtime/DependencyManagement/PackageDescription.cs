using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dnx.Runtime
{
    public class PackageDescription : LibraryDescription
    {
        public PackageDescription(
            LibraryRange requestedRange, 
            LockFilePackageLibrary package, 
            LockFileTargetLibrary lockFileLibrary, 
            IEnumerable<LibraryDependency> dependencies, 
            bool resolved, 
            bool compatible)
            : base(
                  requestedRange,
                  new LibraryIdentity(package.Name, package.Version, isGacOrFrameworkReference: false),
                  path: null,
                  type: LibraryTypes.Package,
                  dependencies: dependencies,
                  assemblies: Enumerable.Empty<string>(),
                  framework: null)
        {
            Library = package;
            Target = lockFileLibrary;
            Resolved = resolved;
            Compatible = compatible;
        }

        public LockFileTargetLibrary Target { get; set; }
        public LockFilePackageLibrary Library { get; set; }
    }
}
