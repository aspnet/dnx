// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Dnx.Runtime
{
    public class PathSearchBasedProjectResolver : IProjectResolver
    {
        private readonly HashSet<string> _searchPaths = new HashSet<string>();
        private readonly ILookup<string, Project> _projects;

        public PathSearchBasedProjectResolver(string projectDir)
        {
            var rootPath = ResolveRootDirectory(projectDir);
            _projects = FindProjects(projectDir, rootPath);
        }

        public PathSearchBasedProjectResolver(string projectDir, string rootDir)
        {
            _projects = FindProjects(projectDir, rootDir);
        }

        public IEnumerable<string> SearchPaths
        {
            get { return _searchPaths; }
        }

        public bool TryResolveProject(string name, out Project project)
        {
            if (_projects.Contains(name))
            {
                var candidates = _projects[name];

                if (candidates.Count() > 1)
                {
                    var allCandidatePaths = string.Join(Environment.NewLine, candidates.Select(x => x.ProjectDirectory).OrderBy(x => x));
                    throw new InvalidOperationException(
                        $"The project name '{name}' is ambiguous between the following projects:{Environment.NewLine}{allCandidatePaths}");
                }
                else
                {
                    project = candidates.Single();
                    return true;
                }
            }
            else
            {
                project = null;
                return false;
            }
        }

        private ILookup<string, Project> FindProjects(string projectDir, string rootDir)
        {
            _searchPaths.Add(Path.GetDirectoryName(projectDir));

            GlobalSettings global;
            if (GlobalSettings.TryGetGlobalSettings(rootDir, out global))
            {
                foreach (var sourcePath in global.ProjectSearchPaths)
                {
                    _searchPaths.Add(Path.GetFullPath(Path.Combine(rootDir, sourcePath)));
                }
            }

            var result = new List<Project>();
            var searched = new HashSet<string>();
            foreach (var path in _searchPaths)
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }

                if (FindProject(path, result, searched))
                {
                    // If a project has been found at one folder, stop searching for other projects under.
                    continue;
                }

                foreach (var subFolder in Directory.GetDirectories(path))
                {
                    if (!searched.Add(subFolder))
                    {
                        continue;
                    }

                    Project project;
                    if (Project.TryGetProject(subFolder, out project))
                    {
                        result.Add(project);
                    }
                }
            }

            return result.ToLookup(project => project.Name);
        }

        private bool FindProject(string directory, List<Project> projects, HashSet<string> searchedPath)
        {
            if (searchedPath.Add(directory))
            {
                Project project;
                if (Project.TryGetProject(directory, out project))
                {
                    projects.Add(project);
                    return true;
                }
            }

            return false;
        }

        public static string ResolveRootDirectory(string projectPath)
        {
            var di = new DirectoryInfo(projectPath);

            while (di.Parent != null)
            {
                var globalJsonPath = Path.Combine(di.FullName, GlobalSettings.GlobalFileName);

                if (File.Exists(globalJsonPath))
                {
                    return di.FullName;
                }

                di = di.Parent;
            }

            // If we don't find any files then make the project folder the root
            return projectPath;
        }
    }
}
