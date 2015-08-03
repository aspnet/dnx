using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;

namespace Microsoft.Dnx.Tooling
{
    public interface IWalkProvider
    {
        bool IsHttp { get; }

        Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, FrameworkName targetFramework, bool includeUnlisted);
        Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework);
        Task<RuntimeFile> GetRuntimes(WalkProviderMatch match, FrameworkName targetFramework);
        Task CopyToAsync(WalkProviderMatch match, Stream stream);
    }
}
