using System;

namespace Microsoft.Framework.Runtime.Infrastructure
{
    [AssemblyNeutral]
    public interface IServiceProviderLocator
    {
        IServiceProvider ServiceProvider { get; set; }
    }
}
