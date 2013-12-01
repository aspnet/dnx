using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class LoadContext
    {
        public LoadContext(string assemblyName, FrameworkName targetFramework)
        {
            AssemblyName = assemblyName;
            TargetFramework = targetFramework;
        }

        public string AssemblyName { get; private set; }

        public FrameworkName TargetFramework { get; private set; }

        public string OutputPath { get; set; }

        // REVIEW: Does this mean we need a new method?
        public bool SkipAssemblyLoad { get; set; }

        public PackageBuilder PackageBuilder { get; set; }

        public PackageBuilder SymbolPackageBuilder { get; set; }

        public IList<string> ArtifactPaths { get; set; }
    }
}
