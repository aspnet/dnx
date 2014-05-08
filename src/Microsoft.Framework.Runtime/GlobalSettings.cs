using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public class GlobalSettings
    {
        public const string GlobalFileName = "global.json";

        public IList<string> SourcePaths { get; private set; }

        public static bool TryGetGlobalSettings(string path, out GlobalSettings solution)
        {
            solution = null;

            string solutionPath = null;

            if (Path.GetFileName(path) == GlobalFileName)
            {
                solutionPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasGlobalFile(path))
            {
                return false;
            }
            else
            {
                solutionPath = Path.Combine(path, GlobalFileName);
            }

            solution = new GlobalSettings();

            string json = File.ReadAllText(solutionPath);
            var settings = JObject.Parse(json);
            var sources = settings["sources"];

            solution.SourcePaths = sources == null ? new string[] { } : sources.ToObject<string[]>();

            return true;
        }

        private static bool HasGlobalFile(string path)
        {
            string projectPath = Path.Combine(path, GlobalFileName);

            return File.Exists(projectPath);
        }

    }
}
