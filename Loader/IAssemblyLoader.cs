using System.Reflection;

namespace Loader
{

    public interface IAssemblyLoader
    {
        Assembly Load(string name);
    }

}
