using System.Reflection;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IAssemblyLoader
    {
        Assembly Load(LoadOptions options);
    }
}
