using System;
using System.IO;
using System.Linq;

namespace Microsoft.Net.Runtime
{
    public class DefaultGlobalAssemblyCache : IGlobalAssemblyCache
    {
        // REVIEW: Is this logic correct 100%?
        public bool TryResolvePartialName(string name, out string assemblyLocation)
        {
            var gacFolders = new[] { IntPtr.Size == 4 ? "GAC_32" : "GAC_64", "GAC_MSIL" };
            string windowsFolder = Environment.GetEnvironmentVariable("WINDIR");

            foreach (var folder in gacFolders)
            {
                string gacPath = Path.Combine(windowsFolder,
                                             @"Microsoft.NET\assembly",
                                             folder,
                                             name);

                var di = new DirectoryInfo(gacPath);

                if (!di.Exists)
                {
                    continue;
                }

                var match = di.EnumerateFiles("*.dll", SearchOption.AllDirectories)
                                .FirstOrDefault(d => Path.GetFileNameWithoutExtension(d.Name).Equals(name, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                {
                    assemblyLocation = match.FullName;
                    return true;
                }
            }

            assemblyLocation = null;
            return false;
        }

        public bool Contains(string name)
        {
            string assemblyLocation;
            return TryResolvePartialName(name, out assemblyLocation);
        }

        public bool IsInGac(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path);

            return Contains(name);
        }
    }
}
