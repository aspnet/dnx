using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class LoadOptions
    {
        public string OutputPath { get; set; }

        public string AssemblyName { get; set; }

        public bool Clean { get; set; }

        public PackageBuilder PackageBuilder { get; set; }

        public PackageBuilder SymbolPackageBuilder { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public IList<string> Artifacts { get; set; }
    }
}
