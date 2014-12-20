// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Framework.Runtime
{
    public class ProjectResolver : IProjectResolver
    {
        private readonly IList<string> _searchPaths;

        public ProjectResolver(string projectPath, string rootPath)
        {
            // We could find all project.json files in the search paths up front here
            _searchPaths = ResolveSearchPaths(projectPath, rootPath).ToList();
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

            foreach (var searchPath in _searchPaths)
            {
                var projectPath = Path.Combine(searchPath, name);

                if (Project.TryGetProject(projectPath, out project))
                {
                    return true;
                }
            }

            return false;
        }
        
        private IEnumerable<string> ResolveSearchPaths(string projectPath, string rootPath)
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

            return paths.Distinct();
        }

        public static string ResolveRootDirectory(string projectPath)
        {
            var di = new DirectoryInfo(projectPath);

            while (di.Parent != null)
            {
                if (di.EnumerateFiles(GlobalSettings.GlobalFileName).Any() ||
                    di.EnumerateFiles("*.sln").Any())
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
