using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public interface IDependencyProvider
    {
        LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework);
        void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework);
    }
}
