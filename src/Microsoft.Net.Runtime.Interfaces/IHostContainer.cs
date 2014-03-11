
using System;

namespace Microsoft.Net.Runtime
{
    [AssemblyNeutral]
    public interface IHostContainer
    {
        IDisposable AddHost(IHost host);
    }
}
