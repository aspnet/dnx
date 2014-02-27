using System.Runtime.Versioning;

namespace Microsoft.Net.Runtime.Loader
{
    public interface IDependencyExporter
    {
        IDependencyExport GetDependencyExport(string name, FrameworkName targetFramework);
    }
}
