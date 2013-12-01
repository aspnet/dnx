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
            CreateArtifacts = true;
        }

        public string AssemblyName { get; private set; }

        public FrameworkName TargetFramework { get; private set; }

        public string OutputPath { get; set; }

        public bool CreateArtifacts { get; set; }

        public PackageBuilder PackageBuilder { get; set; }

        public PackageBuilder SymbolPackageBuilder { get; set; }

        public IList<string> ArtifactPaths { get; set; }
    }
}
