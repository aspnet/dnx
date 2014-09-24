using System;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Summary description for PackageAssembly
    /// </summary>
    public class PackageAssembly
    {
        public string Path { get; set; }
        public string RelativePath { get; set; }
        public LibraryDescription Library { get; set; }
    }
}