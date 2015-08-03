using System;
using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Dnx.Runtime
{
    public class ProjectGraphProvider : IProjectGraphProvider
    {
        private readonly IServiceProvider _hostServices;

        public ProjectGraphProvider(IServiceProvider hostServices)
        {
            _hostServices = hostServices;
        }

        public IEnumerable<LibraryDescription> GetProjectGraph(Project project, FrameworkName targetFramework, string configuration)
        {
            // TODO: Cache sub-graph walk?

            // Create a child app context for this graph walk
            var context = new ApplicationHostContext(
                _hostServices,
                project.ProjectDirectory,
                packagesDirectory: null,
                configuration: configuration,
                targetFramework: targetFramework);

            // Walk the graph
            context.DependencyWalker.Walk(project.Name, project.Version, targetFramework);

            // Return the results
            return context.DependencyWalker.Libraries;
        }
    }
}