using System;
using System.IO;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Dnx.Testing
{
    public static class ProjectExtensions
    {
        public static string GetBinPath(this Runtime.Project project)
        {
            return Path.Combine(project.ProjectDirectory, "bin");
        }

        public static string GetLocalPackagesDir(this Runtime.Project project)
        {
            return Path.Combine(project.ProjectDirectory, "packages");
        }

        public static void UpdateProjectFile(this Runtime.Project project, Action<JObject> updateContents)
        {
            JsonUtils.UpdateJson(project.ProjectFilePath, updateContents);
        }
    }
}
