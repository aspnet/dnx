using System;
using System.Reflection;
using NuGet.Versioning;

namespace Microsoft.Framework.Runtime.Hosting
{
    internal class AssemblyUtils
    {
        internal static NuGetVersion GetAssemblyVersion(string path)
        {
#if ASPNET50
            return new NuGetVersion(AssemblyName.GetAssemblyName(path).Version);
#else
            return new NuGetVersion(System.Runtime.Loader.AssemblyLoadContext.GetAssemblyName(path).Version);
#endif
        }
    }
}