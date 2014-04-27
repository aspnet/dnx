using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Net.PackageManager.Restore.NuGet
{
    public interface IPackageFeed
    {
        Task<IEnumerable<PackageInfo>> FindPackagesByIdAsync(string id);
        Task<Stream> OpenNupkgStreamAsync(PackageInfo package);
        Task<Stream> OpenNuspecStreamAsync(PackageInfo package);
    }
}
