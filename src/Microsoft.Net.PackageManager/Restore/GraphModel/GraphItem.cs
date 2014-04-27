using Microsoft.Net.Runtime;
using System;
using System.Collections.Generic;
using System.Web;

namespace Microsoft.Net.PackageManager
{
    public class GraphItem
    {
        public WalkProviderMatch Match { get; set; }
        public IEnumerable<Library> Dependencies { get; set; }
    }
}
