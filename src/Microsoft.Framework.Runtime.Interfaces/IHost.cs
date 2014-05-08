using System;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IHost : IDisposable
    {
        Assembly Load(string name);
    }
}
