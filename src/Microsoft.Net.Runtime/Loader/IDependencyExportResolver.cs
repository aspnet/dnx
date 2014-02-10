using System.Runtime.Versioning;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IDependencyExportResolver
    {
        DependencyExport GetDependencyExport(string name, FrameworkName targetFramework);
    }
}
