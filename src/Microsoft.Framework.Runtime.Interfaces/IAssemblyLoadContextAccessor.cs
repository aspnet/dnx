using System;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IAssemblyLoadContextAccessor
    {
        IAssemblyLoadContext GetLoadContext(Assembly assembly);
    }

}