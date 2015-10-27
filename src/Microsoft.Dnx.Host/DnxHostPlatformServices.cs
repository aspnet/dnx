using System;
using Microsoft.Dnx.Runtime.Loader;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Host
{
    internal class DnxHostPlatformServices : PlatformServices
    {
        public DnxHostPlatformServices(HostApplicationEnvironment applicationEnvironment, 
                                       IRuntimeEnvironment runtimeEnvironment, 
                                       LoaderContainer container, 
                                       LoadContextAccessor accessor)
        {
            Application = applicationEnvironment;
            Runtime = runtimeEnvironment;
            AssemblyLoaderContainer = container;
            AssemblyLoadContextAccessor = accessor;
        }

        public override IApplicationEnvironment Application { get; }

        public override IAssemblyLoadContextAccessor AssemblyLoadContextAccessor { get; }

        public override IAssemblyLoaderContainer AssemblyLoaderContainer { get; }

        public override ILibraryManager LibraryManager { get; }

        public override IRuntimeEnvironment Runtime { get; }
    }
}