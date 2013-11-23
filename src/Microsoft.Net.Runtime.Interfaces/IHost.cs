using System;
using System.Reflection;

namespace Microsoft.Net.Runtime
{
    public interface IHost : IDisposable
    {
        Assembly GetEntryPoint();

        Assembly Load(string name);
    }
}
