using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;

namespace Loader
{
    public class MSBuildProjectAssemblyLoader : IAssemblyLoader
    {
        private readonly string _solutionDir;

        public MSBuildProjectAssemblyLoader(string solutionDir)
        {
            _solutionDir = solutionDir;
        }

        public Assembly Load(string name)
        {
            string projectFile = Path.Combine(_solutionDir, name, name + ".csproj");

            if (!File.Exists(projectFile))
            {
                return null;
            }

            var projectCollection = new ProjectCollection();
            var properties = new Dictionary<string, string>();
            properties.Add("Configuration", "Debug");
            properties.Add("Platform", "AnyCPU");

            projectCollection.LoadProject(projectFile);

            var buildRequest = new BuildRequestData(projectFile,
                                                    properties, null, new string[] { "Build" }, null);

            var parameters = new BuildParameters(projectCollection)
            {
                // Loggers = new List<ILogger> { new ConsoleLogger() }
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
    }
}
