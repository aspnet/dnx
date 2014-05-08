using System.Collections.Generic;
using System.Runtime.Versioning;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class LoadContext
    {
        public LoadContext(string assemblyName, FrameworkName targetFramework)
        {
            AssemblyName = assemblyName;
            TargetFramework = targetFramework;
        }

        public string AssemblyName { get; private set; }

        public FrameworkName TargetFramework { get; private set; }
    }
}
