using System;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Summary description for LibraryKey
    /// </summary>
    internal class LibraryKey : ILibraryKey
    {
        public string Name { get; set; }
        public FrameworkName TargetFramework { get; set; }
        public string Configuration { get; set; }
        public string Aspect { get; set; }
    }
}