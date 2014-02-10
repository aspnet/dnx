using System.Runtime.Versioning;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IDependencyImpactResolver
    {
        DependencyImpact GetDependencyImpact(string name, FrameworkName targetFramework);
    }
}
