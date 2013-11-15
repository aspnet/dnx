using System.Collections.Generic;
using NuGet;

namespace Loader
{

    public interface IDependencyResolver
    {
        IEnumerable<Dependency> GetDependencies(string name, SemanticVersion version);
        void Initialize(IEnumerable<Dependency> dependencies);
    }

}
