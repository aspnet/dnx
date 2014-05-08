
using System.Runtime.Versioning;

namespace Microsoft.Framework.Project
{
    public class BuildOptions
    {
        public string OutputDir { get; set; }
        public string ProjectDir { get; set; }
        public FrameworkName RuntimeTargetFramework { get; set; }
        public bool CopyDependencies { get; set; }
        public bool GenerateNativeImages { get; set; }
        public string RuntimePath { get; set; }
        public string CrossgenPath { get; set; }
    }
}
