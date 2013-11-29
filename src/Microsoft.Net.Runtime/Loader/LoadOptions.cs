using System.Collections.Generic;
using System.Runtime.Versioning;
namespace Microsoft.Net.Runtime
{
    public class LoadOptions
    {
        public string OutputPath { get; set; }

        public string AssemblyName { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public IList<string> CleanArtifacts { get; set; }
    }
}
