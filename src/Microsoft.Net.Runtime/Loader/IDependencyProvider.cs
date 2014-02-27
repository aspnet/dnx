using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IDependencyProvider
    {
        DependencyDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework);
        void Initialize(IEnumerable<DependencyDescription> dependencies, FrameworkName targetFramework);
    }
}
