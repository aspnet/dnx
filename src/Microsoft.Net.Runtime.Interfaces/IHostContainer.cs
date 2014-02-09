
using System;

namespace Microsoft.Net.Runtime
{
    public interface IHostContainer
    {
        IDisposable AddHost(IHost host);
    }
}
