using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IPackageLoader : IAssemblyLoader
    {
        DependencyDescription GetDescription(string name, SemanticVersion version, FrameworkName frameworkName);
        void Initialize(IEnumerable<DependencyDescription> packages, FrameworkName frameworkName);
    }
}
