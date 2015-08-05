// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.Dnx.Runtime
{
    public class LockFileBasedProjectResolver : IProjectResolver
    {
        private readonly LockFile _lockFile;
        private readonly IDictionary<string, ProjectInformation> _projects;
        private readonly IEnumerable<string> _searchPath;

        public LockFileBasedProjectResolver(LockFile lockFile, Project currentProject, string rootDirectory)
        {
            _lockFile = lockFile;

            _projects = GetProjects(lockFile, currentProject.ProjectDirectory);
            _projects[currentProject.Name] = new ProjectInformation(currentProject);

            _searchPath = GetSearchPaths(rootDirectory, currentProject.ProjectDirectory);
        }

        public IEnumerable<string> SearchPaths
        {
            get { return _searchPath; }
        }

        public bool TryResolveProject(string name, out Project project)
        {
            project = null;

            ProjectInformation projectInfo;
            if (_projects.TryGetValue(name, out projectInfo))
            {
                project = projectInfo.Project;
            }

            return project != null;
        }

        private static IDictionary<string, ProjectInformation> GetProjects(LockFile lockFile, string baseDirectory)
        {
            return lockFile.ProjectLibraries.ToDictionary(
                keySelector: library => library.Name,
                elementSelector: library => new ProjectInformation(library, baseDirectory));
        }

        private static HashSet<string> GetSearchPaths(string rootDirectory, string projectPath)
        {
            var result = new HashSet<string>();
            result.Add(Path.GetDirectoryName(projectPath));

            GlobalSettings global;
            if (GlobalSettings.TryGetGlobalSettings(rootDirectory, out global))
            {
                foreach (var sourcePath in global.ProjectSearchPaths)
                {
                    result.Add(Path.Combine(rootDirectory, sourcePath));
                }
            }

            return result;
        }

        private class ProjectInformation
        {
            private Project _project;
            private bool _initalized;
            private object _lock = new object();

            public ProjectInformation(LockFileProjectLibrary library, string baseDirectory)
            {
                ProjectDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.Combine(baseDirectory, library.Path)));
            }

            public ProjectInformation(Project project)
            {
                ProjectDirectory = project.ProjectDirectory;
                _project = project;
                _initalized = true;
            }

            public Project Project
            {
                get
                {
                    return LazyInitializer.EnsureInitialized<Project>(ref _project, ref _initalized, ref _lock, () =>
                    {
                        Project project;
                        Project.TryGetProject(ProjectDirectory, out project);

                        return project;
                    });
                }
            }

            public string ProjectDirectory { get; }
        }
    }
}
