using System;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IAssemblyNeutralInterfaceCache
    {
        Assembly GetAssembly(string name);
        bool IsLoaded(string name);
        void AddAssembly(string name, Assembly assembly);
    }
}