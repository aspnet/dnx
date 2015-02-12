using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    public interface IAssemblyLoadContextAccessor
    {
        IAssemblyLoadContext Default { get; }
        IAssemblyLoadContext GetLoadContext(Assembly assembly);
    }
}