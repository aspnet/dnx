
namespace Microsoft.Net.Project
{
    public class BuildOptions
    {
        public string OutputDir { get; set; }
        public string ProjectDir { get; set; }
        public string RuntimeTargetFramework { get; set; }
        public bool CopyDependencies { get; set; }
    }
}
