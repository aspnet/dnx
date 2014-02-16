using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class BuildContext
    {
        public BuildContext(string assemblyName, FrameworkName targetFramework)
        {
            AssemblyName = assemblyName;
            TargetFramework = targetFramework;
        }

        public string AssemblyName { get; private set; }

        public FrameworkName TargetFramework { get; private set; }

        public string OutputPath { get; set; }

        public bool CopyDependencies { get; set; }

        public PackageBuilder PackageBuilder { get; set; }

        public PackageBuilder SymbolPackageBuilder { get; set; }
    }
}
