using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IAssemblyLoadContextAccessor
    {
        IAssemblyLoadContext Default { get; }
        IAssemblyLoadContext GetLoadContext(Assembly assembly);
    }
}