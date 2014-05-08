using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Framework.PackageManager
{
    public class RestoreContext
    {
        public RestoreContext()
        {
            FindLibraryCache = new Dictionary<Library, Task<GraphItem>>();
        }

        public FrameworkName FrameworkName { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<Library, Task<GraphItem>> FindLibraryCache { get; private set; }
    }
}
