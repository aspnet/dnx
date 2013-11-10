using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Loader
{
    public class MSBuildProjectAssemblyLoader : IAssemblyLoader
    {
        private readonly string _solutionDir;
        private readonly IFileWatcher _watcher;

        public MSBuildProjectAssemblyLoader(string solutionDir, IFileWatcher watcher)
        {
            _solutionDir = solutionDir;
            _watcher = watcher;
        }

        public Assembly Load(LoadOptions options)
        {
            string name = options.AssemblyName;

            string projectDir = Path.Combine(_solutionDir, name);

            // Bail if there's a project settings file
            if (RoslynProject.HasProjectFile(projectDir))
            {
                return null;
            }

            var projectCollection = new ProjectCollection();
            string projectFile = Path.Combine(projectDir, name + ".csproj");
            if (!File.Exists(projectFile))
            {
                // This is pretty slow
                if (!TryGetProjectFile(name, projectCollection, out projectFile))
                {
                    return null;
                }
            }

            var properties = new Dictionary<string, string>();
            properties.Add("Configuration", "Debug");
            properties.Add("Platform", "AnyCPU");

            WatchProject(projectDir, projectFile, projectCollection);

            var buildRequest = new BuildRequestData(projectFile,
                                                    properties, null, new string[] { "Build" }, null);

            var parameters = new BuildParameters(projectCollection)
            {
                Loggers = new List<ILogger> { new ConsoleLogger(LoggerVerbosity.Quiet) }
            };

            BuildResult buildResult = BuildManager.DefaultBuildManager.Build(parameters, buildRequest);


            if (buildResult.OverallResult == BuildResultCode.Success)
            {
                // REVIEW: This needs hardening
                TargetResult result;
                if (buildResult.ResultsByTarget.TryGetValue("Build", out result))
                {
                    var item = result.Items.FirstOrDefault();

                    if (item == null)
                    {
                        return null;
                    }

                    return Assembly.LoadFile(item.ItemSpec);
                }
            }

            return null;
        }

        private bool TryGetProjectFile(string name, ProjectCollection projectCollection, out string projectFile)
        {
            string[] candidates = new string[] { 
                Path.Combine(_solutionDir, name + ".sln"),
                Path.Combine(_solutionDir, name, name + ".sln"),
            };

            foreach (var solutionFile in candidates)
            {
                if (File.Exists(solutionFile))
                {
                    return TryFindProjectInSolution(solutionFile, name, projectCollection, out projectFile);
                }
            }

            projectFile = null;
            return false;
        }

        private bool TryFindProjectInSolution(string solutionFile, string name, ProjectCollection projectCollection, out string projectFile)
        {
            string wrapperContent = Microsoft.Build.BuildEngine.SolutionWrapperProject.Generate(solutionFile, toolsVersionOverride: null, projectBuildEventContext: null);
            using (XmlTextReader xmlReader = new XmlTextReader(new StringReader(wrapperContent)))
            {
                Project sln = projectCollection.LoadProject(xmlReader);

                var projects = GetOrderedProjectReferencesFromWrapper(sln).ToList();

                foreach (var p in projects)
                {
                    if (Path.GetFileNameWithoutExtension(p.EvaluatedInclude).Equals(name, StringComparison.OrdinalIgnoreCase))
                    {
                        projectFile = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(solutionFile), p.EvaluatedInclude));
                        return true;
                    }
                }
            }

            projectFile = null;
            return false;
        }
        private static IEnumerable<ProjectItem> GetOrderedProjectReferencesFromWrapper(Project solutionWrapperProject)
        {
            int buildLevel = 0;
            while (true)
            {
                var items = solutionWrapperProject.GetItems(String.Format("BuildLevel{0}", buildLevel));
                if (items.Count == 0)
                {
                    yield break;
                }
                foreach (var item in items)
                {
                    yield return item;
                }

                buildLevel++;
            }
        }

        private void WatchProject(string projectDir, string projectFile, ProjectCollection projectCollection)
        {
            // We're already watching this file
            if (!_watcher.Watch(projectFile))
            {
                return;
            }

            var project = projectCollection.LoadProject(projectFile);

            foreach (var contentItem in project.GetItems("Compile"))
            {
                var path = Path.Combine(projectDir, contentItem.EvaluatedInclude);
                _watcher.Watch(Path.GetFullPath(path));
            }

            // Watch project references
            foreach (var item in project.GetItems("ProjectReference"))
            {
                string path = Path.GetFullPath(Path.Combine(projectDir, item.EvaluatedInclude));
                WatchProject(projectDir, path, projectCollection);
            }
        }
    }
}
