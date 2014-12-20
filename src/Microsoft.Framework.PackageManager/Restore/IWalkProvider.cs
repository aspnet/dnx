using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    public interface IWalkProvider
    {
        bool IsHttp { get; }

        Task<WalkProviderMatch> FindLibraryByName(string name, FrameworkName targetFramework);
        Task<WalkProviderMatch> FindLibraryByVersion(Library library, FrameworkName targetFramework);
        Task<WalkProviderMatch> FindLibraryBySnapshot(Library library, FrameworkName targetFramework);
        Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework);
        Task CopyToAsync(WalkProviderMatch match, Stream stream);
    }

}