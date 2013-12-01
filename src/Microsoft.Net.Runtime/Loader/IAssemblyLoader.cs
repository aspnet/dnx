using System.Reflection;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IAssemblyLoader
    {
        AssemblyLoadResult Load(LoadContext loadContext);
    }
}
