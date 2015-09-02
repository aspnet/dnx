using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Dnx.Testing
{
    public class DnuPackOutput : ExecResult
    {
        public DnuPackOutput(string outputPath, string packageName, string configuration)
        {
            RootPath = outputPath;
            PackageName = packageName;
            Configuration = configuration;
            var basePath = Path.Combine(RootPath, Configuration);
            PackagePath = Directory.EnumerateFiles(basePath, $"*{Constants.PackageExtension}")
                .Where(x => Path.GetFileName(x).StartsWith(packageName))
                .FirstOrDefault(x => !x.EndsWith($"*.symbols{Constants.PackageExtension}"));
            if (string.IsNullOrEmpty(PackagePath))
            {
                throw new InvalidOperationException($"Could not find NuGet package in '{basePath}'");
            }
        }

        public string RootPath { get; private set; }

        public string Configuration { get; private set; }

        public string PackageName { get; private set; }

        public string PackagePath { get; private set; }

        public string GetAssemblyPath(FrameworkName framework)
        {
            var shortName = VersionUtility.GetShortFrameworkName(framework);
            return Path.Combine(RootPath, Configuration, shortName, $"{PackageName}.dll");
        }
    }
}
