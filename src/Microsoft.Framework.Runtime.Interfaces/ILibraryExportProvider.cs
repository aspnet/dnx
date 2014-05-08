using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface ILibraryExportProvider
    {
        ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework);
    }
}
