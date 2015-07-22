using System;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling
{
    public class WalkProviderMatch
    {
        public IWalkProvider Provider { get; set; }
        public LibraryIdentity Library { get; set; }
        public string Path { get; set; }
    }

}