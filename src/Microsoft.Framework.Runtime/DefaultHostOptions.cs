
using System.Runtime.Versioning;
namespace Microsoft.Framework.Runtime
{
    public class DefaultHostOptions
    {
        public string ApplicationName { get; set; }

        public string ApplicationBaseDirectory { get; set; }

        public string PackageDirectory { get; set; }

        public FrameworkName TargetFramework { get; set; }

        public bool WatchFiles { get; set; }

        public bool UseCachedCompilations { get; set; }

        public DefaultHostOptions()
        {
            UseCachedCompilations = true;
        }
    }
}
