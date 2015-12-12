using System.Runtime.Versioning;

namespace Microsoft.Dnx.ApplicationHost
{
    public class DefaultHostOptions
    {
        public string ApplicationBaseDirectory { get; set; }
        public string ApplicationName { get; set; }
        public int? CompilationServerPort { get; set; }
        public string Configuration { get; set; }
        public FrameworkName TargetFramework { get; set; }
    }
}
