using System;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.ApplicationHost
{
    internal class ApplicationHostPlatformServices : PlatformServices
    {
        private readonly PlatformServices _previous;

        public ApplicationHostPlatformServices(PlatformServices previous, 
                                               ApplicationEnvironment applicationEnvironment, 
                                               RuntimeLibraryManager runtimeLibraryManager)
        {
            _previous = previous;
            LibraryManager = runtimeLibraryManager;
            Application = applicationEnvironment;
        }

        public override IApplicationEnvironment Application { get; }

        public override ILibraryManager LibraryManager { get; }

        public override IAssemblyLoadContextAccessor AssemblyLoadContextAccessor => _previous.AssemblyLoadContextAccessor;

        public override IAssemblyLoaderContainer AssemblyLoaderContainer => _previous.AssemblyLoaderContainer;

        public override IRuntimeEnvironment Runtime => _previous.Runtime;
    }
}