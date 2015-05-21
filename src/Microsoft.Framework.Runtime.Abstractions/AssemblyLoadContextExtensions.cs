using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    public static class AssemblyLoadContextExtensions
    {
        public static Assembly Load(this IAssemblyLoadContext loadContext, string name)
        {
            return loadContext.Load(new AssemblyName(name));
        }
    }
}
