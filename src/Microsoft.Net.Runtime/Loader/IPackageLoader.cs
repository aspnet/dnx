using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IPackageLoader : IAssemblyLoader
    {
        IEnumerable<PackageReference> GetDependencies(string name, SemanticVersion version, FrameworkName frameworkName);
        void Initialize(IEnumerable<PackageReference> packages, FrameworkName frameworkName);
    }
}
