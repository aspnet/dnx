using System.Runtime.Versioning;

namespace Microsoft.Net.Project
{
    public class PackOptions
    {
        public string OutputDir { get; set; }

        public string ProjectDir { get; set; }
        
        public FrameworkName RuntimeTargetFramework { get; set; }
    }
}