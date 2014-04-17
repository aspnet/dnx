using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Net.Runtime
{
    public class ProjectResolver : IProjectResolver
    {
        private readonly IList<string> _searchPaths;

        public ProjectResolver(string projectPath, string rootPath)
        {
            // We could find all project.json files in the search paths up front here
            _searchPaths = ResolveSearchPaths(projectPath, rootPath);
        }

        public bool TryResolveProject(string name, out Project project)
        {
            project = _searchPaths.Select(path => Path.Combine(path, name))
                                  .Select(path => GetProject(path))
                                  .FirstOrDefault(p => p != null);

            return project != null;
        }

        private Project GetProject(string path)
        {
            Project project;
            Project.TryGetProject(path, out project);
            return project;
        }

        private IList<string> ResolveSearchPaths(string projectPath, string rootPath)
        {
            var paths = new List<string>
            {
                Path.GetDirectoryName(projectPath)
            };

            GlobalSettings global;

            if (GlobalSettings.TryGetGlobalSettings(rootPath, out global))
            {
                foreach (var sourcePath in global.SourcePaths)
                {
                    paths.Add(Path.Combine(rootPath, sourcePath));
                }
            }

            return paths;
        }
    }
}
