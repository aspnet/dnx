using System.Runtime.Versioning;

namespace Microsoft.Net.Runtime.Services
{
    [AssemblyNeutral]
    public interface IProjectMetadataProvider
    {
        IProjectMetadata GetProjectMetadata(string name, FrameworkName targetFramework);
    }
}
