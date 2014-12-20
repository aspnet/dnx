using System;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    public class WalkProviderMatch
    {
        public IWalkProvider Provider { get; set; }
        public Library Library { get; set; }
        public string Path { get; set; }
    }

}