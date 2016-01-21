using System;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Host
{
    internal class DnxHostPlatformServices : PlatformServices
    {
        public DnxHostPlatformServices(HostApplicationEnvironment applicationEnvironment, 
                                       IRuntimeEnvironment runtimeEnvironment)
        {
            Application = applicationEnvironment;
            Runtime = runtimeEnvironment;
        }

        public override IApplicationEnvironment Application { get; }

        public override IRuntimeEnvironment Runtime { get; }
    }
}