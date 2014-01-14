using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IPackageLoader : IAssemblyLoader
    {
        PackageDetails GetDetails(string name, SemanticVersion version, FrameworkName frameworkName);
        void Initialize(IEnumerable<PackageReference> packages, FrameworkName frameworkName);
    }
}
