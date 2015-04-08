using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.PackageManager.Restore.RuntimeModel;
using NuGet.Frameworks;
using LibraryRange = NuGet.LibraryModel.LibraryRange;
using LibraryDependency = NuGet.LibraryModel.LibraryDependency;

namespace Microsoft.Framework.PackageManager
{
    public interface IWalkProvider
    {
        bool IsHttp { get; }

        Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, NuGetFramework targetFramework);
        Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, NuGetFramework targetFramework);
        Task<RuntimeFile> GetRuntimes(WalkProviderMatch match, NuGetFramework targetFramework);
        Task CopyToAsync(WalkProviderMatch match, Stream stream);
    }
}
