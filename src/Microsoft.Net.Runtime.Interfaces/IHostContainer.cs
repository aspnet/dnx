
using System;
using System.Reflection;

namespace Microsoft.Net.Runtime
{
    public interface IHostContainer
    {
        IDisposable AddHost(IHost host);
    }
}
