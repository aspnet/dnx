using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class ProjectReferenceDependencyProvider : IDependencyProvider
    {
        private readonly IProjectResolver _projectResolver;

        public ProjectReferenceDependencyProvider(IProjectResolver projectResolver)
        {
            _projectResolver = projectResolver;
            Dependencies = Enumerable.Empty<LibraryDescription>();
        }

        public IEnumerable<LibraryDescription> Dependencies { get; private set; }

        public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
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

            // This never returns null
            var configDependencies = project.GetTargetFrameworkConfiguration(targetFramework).Dependencies;

            if (VersionUtility.IsDesktop(targetFramework))
            {
                // mscorlib is ok
                configDependencies.Add(new Library { Name = "mscorlib" });

                // TODO: Remove these references (but we need to update the dependent projects first)
                configDependencies.Add(new Library { Name = "System" });
                configDependencies.Add(new Library { Name = "System.Core" });
                configDependencies.Add(new Library { Name = "Microsoft.CSharp" });
            }

            return new LibraryDescription
            {
                Identity = new Library { Name = project.Name, Version = project.Version },
                Dependencies = project.Dependencies.Concat(configDependencies),
            };
        }

        public virtual void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework)
        {
            // PERF: It sucks that we have to do this twice. We should be able to round trip
            // the information from GetDescription
            foreach (var d in dependencies)
            {
                Project project;
                if (_projectResolver.TryResolveProject(d.Identity.Name, out project))
                {
                    d.Path = project.ProjectFilePath;
                    d.Type = "Project";
                }
            }

            Dependencies = dependencies;
        }
    }
}
