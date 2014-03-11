using System;
using System.Reflection;

namespace Microsoft.Net.Runtime
{
    [AssemblyNeutral]
    public interface IHost : IDisposable
    {
        Assembly Load(string name);
    }
}
