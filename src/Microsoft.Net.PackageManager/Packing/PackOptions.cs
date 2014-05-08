using System.Collections.Generic;
using System.Runtime.Versioning;

namespace Microsoft.Net.PackageManager.Packing
{
    public class PackOptions
    {
        public string OutputDir { get; set; }

        public string ProjectDir { get; set; }
        
        public FrameworkName RuntimeTargetFramework { get; set; }

        public bool ZipPackages { get; set; }

        public bool Overwrite { get; set; }

        public IEnumerable<string> Runtimes { get; set; }
    }
}