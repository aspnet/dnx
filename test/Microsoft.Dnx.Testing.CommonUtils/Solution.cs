using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;
using ProjectResolver = Microsoft.Dnx.Tooling.ProjectResolver;

namespace Microsoft.Dnx.Testing
{
    public class Solution
    {
        public Solution(string rootPath)
        {
            RootPath = rootPath;
        }

        public string RootPath { get; private set; }

        public IEnumerable<Runtime.Project> Projects
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

        public Runtime.Project GetProject(string name)
        {
            Runtime.Project project;
            var resolver = new ProjectResolver(RootPath);
            if (!resolver.TryResolveProject(name, out project))
            {
                throw new InvalidOperationException($"Unable to resolve project '{name}' from '{RootPath}'");
            }
            return project;
        }

        public string GetWrapperProjectPath(string name)
        {
            var path = Path.Combine(WrapFolderPath, name, Runtime.Project.ProjectFileName);
            if (!Directory.Exists(path))
            {
                throw new InvalidOperationException($"Unable to find wrapper project {path}");
            }
            return path;
        }

        public string GetCsprojPath(string name)
        {
            return Path.Combine(RootPath, name, $"{name}.csproj");
        }

        private IEnumerable<Runtime.Project> ResolveAllProjects()
        {
            var resolver = new ProjectResolver(RootPath);
            var searchPaths = resolver.SearchPaths;
            foreach (var path in searchPaths.Concat(searchPaths.SelectMany(p => Directory.EnumerateDirectories(p))))
            {
                Runtime.Project project;
                var name = new DirectoryInfo(path).Name;
                if (resolver.TryResolveProject(name, out project))
                {
                    yield return project;
                }
            }
        }
    }
}
