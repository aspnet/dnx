using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.DependencyManagement;

namespace Microsoft.Framework.PackageManager
{
    public interface IWalkProvider
    {
        bool IsHttp { get; }

        Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, FrameworkName targetFramework);
        Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework);
        Task<LockFileLibrary> GetLockFileLibrary(WalkProviderMatch match);
        Task CopyToAsync(WalkProviderMatch match, Stream stream);
    }

}