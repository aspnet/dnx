
namespace Microsoft.Net.Runtime
{
    public class DefaultHostOptions
    {
        public string ProjectDir { get; set; }

        public string TargetFramework { get; set; }

        public bool WatchFiles { get; set; }

        public bool UseCachedCompilations { get; set; }

        public DefaultHostOptions()
        {
            UseCachedCompilations = true;
        }
    }
}
