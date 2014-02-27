using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public class ProjectReferenceDependencyProvider : IDependencyProvider
    {
        private readonly IProjectResolver _projectResolver;

        public ProjectReferenceDependencyProvider(IProjectResolver projectResolver)
        {
            _projectResolver = projectResolver;
            ResolvedDependencies = Enumerable.Empty<DependencyDescription>();
        }

        public IEnumerable<DependencyDescription> ResolvedDependencies { get; private set; }

        public DependencyDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            Project project;

            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }
            else if (version != null && !project.Version.EqualsSnapshot(version))
            {
                return null;
            }

            var config = project.GetTargetFrameworkConfiguration(targetFramework);

            return new DependencyDescription
            {
                Identity = new Dependency { Name = project.Name, Version = project.Version },
                Dependencies = project.Dependencies.Concat(config.Dependencies),
            };
        }


        public virtual void Initialize(IEnumerable<DependencyDescription> dependencies, FrameworkName targetFramework)
        {
            ResolvedDependencies = dependencies;
        }
    }
}
