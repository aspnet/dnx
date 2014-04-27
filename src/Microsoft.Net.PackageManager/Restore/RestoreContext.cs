using Microsoft.Net.Runtime;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Net.PackageManager
{
    public class RestoreContext
    {
        public RestoreContext()
        {
            FindLibraryCache = new Dictionary<Library, Task<GraphItem>>();
        }

        public TargetFrameworkConfiguration TargetFrameworkConfiguration { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<Library, Task<GraphItem>> FindLibraryCache { get; private set; }
    }
}
