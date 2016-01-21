using Microsoft.Dnx.Runtime.Loader;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Host
{
    internal class DnxHostDnxPlatformServices : DnxPlatformServices
    {
        public DnxHostDnxPlatformServices(LoaderContainer container,
            LoadContextAccessor accessor)
        {
            AssemblyLoaderContainer = container;
            AssemblyLoadContextAccessor = accessor;
        }

        public override IAssemblyLoadContextAccessor AssemblyLoadContextAccessor { get; }

        public override IAssemblyLoaderContainer AssemblyLoaderContainer { get; }

        public override ILibraryManager LibraryManager { get; }
    }
}