using System.Runtime.Versioning;

namespace Microsoft.Net.Runtime.Services
{
    [AssemblyNeutral]
    public interface IDependencyRefresher
    {
        void RefreshDependencies(string name, string version, FrameworkName targetFramework);
    }
}
