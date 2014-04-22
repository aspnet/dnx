using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class UnresolvedDependencyProvider : IDependencyProvider
    {
        public IEnumerable<LibraryDescription> UnresolvedDependencies { get; private set; }

        public UnresolvedDependencyProvider()
        {
            UnresolvedDependencies = Enumerable.Empty<LibraryDescription>();
        }

        public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            return new LibraryDescription
            {
                Identity = new Library { Name = name, Version = version },
                Dependencies = Enumerable.Empty<Library>()
            };
        }

        public void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework)
        {
            UnresolvedDependencies = dependencies;
        }
    }
}