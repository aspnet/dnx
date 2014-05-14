using System;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager
{
    // We never care about resolving framework references in kpm restore
    internal class EmptyFrameworkResolver : IFrameworkReferenceResolver
    {
        public bool TryGetAssembly(string name, FrameworkName frameworkName, out string path)
        {
            path = null;
            return false;
        }
    }
}