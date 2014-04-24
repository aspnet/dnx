using System;

namespace Microsoft.Net.Runtime.Infrastructure
{
    [AssemblyNeutral]
    public interface IServiceProviderLocator
    {
        IServiceProvider ServiceProvider { get; set; }
    }
}
