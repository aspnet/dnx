
using System;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IHostContainer
    {
        IDisposable AddHost(IHost host);
    }
}
