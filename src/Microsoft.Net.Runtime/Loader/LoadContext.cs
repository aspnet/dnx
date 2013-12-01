using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class LoadContext
    {
        public string OutputPath { get; set; }

        public string AssemblyName { get; set; }

        // REVIEW: Does this mean we need a new method?
        public bool SkipAssemblyLoad { get; set; }

        public PackageBuilder PackageBuilder { get; set; }

        public PackageBuilder SymbolPackageBuilder { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public IList<string> ArtifactPaths { get; set; }
    }
}
