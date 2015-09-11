using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation
{
    public class ProjectGraphProvider : IProjectGraphProvider
    {
        private readonly IResolvedLibraryCache _libraryCache;

        public ProjectGraphProvider(IResolvedLibraryCache libraryCache)
        {
            _libraryCache = libraryCache;
        }

        public LibraryManager GetProjectGraph(Project project, FrameworkName targetFramework)
        {
            // TODO: Cache sub-graph walk?

            // Create a child app context for this graph walk
            var context = new ApplicationHostContext
            {
                Project = project,
                TargetFramework = targetFramework,
                LibraryCache = _libraryCache,
            };

            ApplicationHostContext.Initialize(context);

            // Return the results
            return context.LibraryManager;
        }
    }
}