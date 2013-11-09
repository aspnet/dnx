using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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

        public Assembly Load(string name)
        {
            string projectDir = Path.Combine(_solutionDir, name);
            string projectFile = Path.Combine(projectDir, name + ".csproj");
            ProjectSettings settings;

            // Bail if there's a project settings file
            if (!File.Exists(projectFile) ||
                ProjectSettings.TryGetSettings(projectFile, out settings))
            {
                return null;
            }

            var projectCollection = new ProjectCollection();
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
