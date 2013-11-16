using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Loader
{

    public interface IDependencyResolver
    {
        IEnumerable<Dependency> GetDependencies(string name, SemanticVersion version, FrameworkName frameworkName);
        void Initialize(IEnumerable<Dependency> dependencies, FrameworkName frameworkName);
    }

}
