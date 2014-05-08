using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
{
    public interface IAssemblyLoader
    {
        AssemblyLoadResult Load(LoadContext loadContext);
    }
}
