using NuGet;
using System;
using System.Collections.Generic;
using System.Web;

namespace Microsoft.Framework.PackageManager
{
    public class PackageInfo
    {
        public string Id { get; set; }
        public SemanticVersion Version { get; set; }
        public string ContentUri { get; set; }
    }
}