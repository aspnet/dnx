// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Testing.Framework
{
    public class Solution
    {
        private Lazy<ProjectResolver> _lazyResolver;
        
        public Solution(string rootPath)
        {
            if (!Directory.Exists(rootPath))
            {
                throw new ArgumentException("Directory was not found", nameof(rootPath)); ;
            }

            RootPath = rootPath;

            _lazyResolver = new Lazy<ProjectResolver>(() => new ProjectResolver(RootPath));
        }

        public string RootPath { get; private set; }

        public IEnumerable<Project> Projects
        {
            get
            {
                return ResolveAllProjects();
            }
        }

        public string GlobalFilePath
        {
            get
            {
                return Path.Combine(RootPath, GlobalSettings.GlobalFileName);
            }
        }

        public string ArtifactsPath
        {
            get
            {
                return Path.Combine(RootPath, "artifacts");
            }
        }

        public string LocalPackagesDir
        {
            get
            {
                return Path.Combine(RootPath, "packages");
            }
        }

        public string SourcePath
        {
            get
            {
                return Path.Combine(RootPath, "src");
            }
        }

        public string WrapFolderPath
        {
            get
            {
                return Path.Combine(RootPath, "wrap");
            }
        }

        public Project GetProject(string name)
        {
            Project project;
            var resolver = _lazyResolver.Value;
            if (!resolver.TryResolveProject(name, out project))
            {
                throw new InvalidOperationException($"Unable to resolve project '{name}' from '{RootPath}'");
            }
            return project;
        }

        public string GetWrapperProjectPath(string name)
        {
            var path = Path.Combine(WrapFolderPath, name, Project.ProjectFileName);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException($"Unable to find wrapper project {path}");
            }
            return path;
        }

        public string GetCsprojPath(string name)
        {
            return Path.Combine(RootPath, name, $"{name}.csproj");
        }

        private IEnumerable<Project> ResolveAllProjects()
        {
            var resolver = _lazyResolver.Value;
            var searchPaths = resolver.SearchPaths;
            foreach (var path in searchPaths.Concat(searchPaths.SelectMany(p => Directory.EnumerateDirectories(p))))
            {
                Project project;
                var name = new DirectoryInfo(path).Name;
                if (resolver.TryResolveProject(name, out project))
                {
                    yield return project;
                }
            }
        }
    }
}
