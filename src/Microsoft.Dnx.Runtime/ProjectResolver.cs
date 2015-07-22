// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Microsoft.Dnx.Runtime
{
    public class ProjectResolver : IProjectResolver
    {
        private readonly HashSet<string> _searchPaths = new HashSet<string>();
        private ILookup<string, ProjectInformation> _projects;

        public ProjectResolver(string projectPath)
        {
            var rootPath = ResolveRootDirectory(projectPath);
            Initialize(projectPath, rootPath);
        }

        public ProjectResolver(string projectPath, string rootPath)
        {
            Initialize(projectPath, rootPath);
        }

        public IEnumerable<string> SearchPaths
        {
            get
            {
                return _searchPaths;
            }
        }

        public bool TryResolveProject(string name, out Project project)
        {
            project = null;

            if (!_projects.Contains(name))
            {
                return false;
            }

            // ProjectInformation.Project is lazily evaluated only once and
            // it returns null when ProjectInformation.FullPath doesn't contain project.json
            var candidates = _projects[name].Where(p => p.Project != null);
            if (candidates.Count() > 1)
            {
                var allCandidatePaths = string.Join(Environment.NewLine, candidates.Select(x => x.FullPath).OrderBy(x => x));
                throw new InvalidOperationException(
                    $"The project name '{name}' is ambiguous between the following projects:{Environment.NewLine}{allCandidatePaths}");
            }

            project = candidates.SingleOrDefault()?.Project;
            return project != null;
        }

        private void Initialize(string projectPath, string rootPath)
        {
            _searchPaths.Add(new DirectoryInfo(projectPath).Parent.FullName);

            GlobalSettings global;

            if (GlobalSettings.TryGetGlobalSettings(rootPath, out global))
            {
                foreach (var sourcePath in global.ProjectSearchPaths)
                {
                    _searchPaths.Add(Path.Combine(rootPath, sourcePath));
                }
            }

            Func<DirectoryInfo, ProjectInformation> dirInfoToProjectInfo = d => new ProjectInformation
            {
                // The name of the folder is the project
                Name = d.Name,
                FullPath = d.FullName
            };

            // Resolve all of the potential projects
            _projects = _searchPaths.Select(path => new DirectoryInfo(path))
                .Where(d => d.Exists)
                .SelectMany(d => new[] { d }.Concat(d.EnumerateDirectories()))
                .Distinct(new DirectoryInfoFullPathComparator())
                .Select(dirInfoToProjectInfo)
                .ToLookup(d => d.Name);
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

        private class ProjectInformation
        {
            private Project _project;
            private bool _initialized;
            private object _lockObj = new object();

            public string Name { get; set; }

            public string FullPath { get; set; }

            public Project Project
            {
                get
                {
                    return LazyInitializer.EnsureInitialized(ref _project, ref _initialized, ref _lockObj, () =>
                    {
                        Project project;
                        Project.TryGetProject(FullPath, out project);
                        return project;
                    });
                }
            }
        }

        private class DirectoryInfoFullPathComparator : IEqualityComparer<DirectoryInfo>
        {
            public bool Equals(DirectoryInfo x, DirectoryInfo y)
            {
                return string.Equals(x.FullName, y.FullName);
            }

            public int GetHashCode(DirectoryInfo obj)
            {
                return obj.FullName.GetHashCode();
            }
        }
    }
}
