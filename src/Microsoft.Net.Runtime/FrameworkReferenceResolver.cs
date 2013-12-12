using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using NuGet;

namespace Microsoft.Net.Runtime
{
    public class FrameworkReferenceResolver : IFrameworkReferenceResolver
    {
        private static IDictionary<FrameworkName, FrameworkInformation> _cache = PopulateCache();

        public IEnumerable<string> GetFrameworkReferences(FrameworkName frameworkName)
        {
            FrameworkInformation frameworkInfo;
            if (_cache.TryGetValue(frameworkName, out frameworkInfo))
            {
                return frameworkInfo.Assemblies.Select(a => a.Name);
            }

            return Enumerable.Empty<string>();
        }

        public IEnumerable<MetadataReference> GetDefaultReferences(FrameworkName frameworkName)
        {
#if DESKTOP
            if (frameworkName.Identifier == VersionUtility.DefaultTargetFramework.Identifier)
            {
                // Do not reference the entire desktop .NET framework by default
                return new[] {
                    ResolveGacAssembly("mscorlib"),
                    ResolveGacAssembly("System"),
                    ResolveGacAssembly("System.Core"),
                    ResolveGacAssembly("Microsoft.CSharp")
                };
            }
#endif

            FrameworkInformation frameworkInfo;
            if (_cache.TryGetValue(frameworkName, out frameworkInfo))
            {
                return frameworkInfo.Assemblies.Select(a => new MetadataFileReference(a.Path));
            }

            throw new InvalidOperationException(String.Format("Unknown target framework '{0}'.", frameworkName));
        }

        private static IDictionary<FrameworkName, FrameworkInformation> PopulateCache()
        {
            var info = new Dictionary<FrameworkName, FrameworkInformation>();
#if DESKTOP
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Reference Assemblies\Microsoft\Framework\");

            PopulateReferenceAssemblies(defaultPath, info);

            // Additional profile paths
            var profilePaths = Environment.GetEnvironmentVariable("PROFILE_PATHS");

            if (profilePaths != null)
            {
                foreach (var profilePath in profilePaths.Split(';'))
                {
                    PopulateReferenceAssemblies(profilePath, info);
                }
            }
#endif
            string klrPath = Environment.GetEnvironmentVariable("KLR_PATH");

            if (!String.IsNullOrEmpty(klrPath))
            {
                // Convention based on install directory
                // ..\..\Framework
                var targetingPacks = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(klrPath), @"..\..\Framework"));

                if (Directory.Exists(targetingPacks))
                {
                    PopulateReferenceAssemblies(targetingPacks, info);
                }
            }

            return info;
        }

#if DESKTOP
        private static MetadataReference ResolveGacAssembly(string name)
        {
            string assemblyLocation;
            if (GlobalAssemblyCache.ResolvePartialName(name, out assemblyLocation) != null)
            {
                return new MetadataFileReference(assemblyLocation);
            }

            throw new InvalidOperationException("Unable to resolve GAC reference");
        }
#endif

        private static void PopulateReferenceAssemblies(string path, IDictionary<FrameworkName, FrameworkInformation> cache)
        {
            var di = new DirectoryInfo(path);

            if (!di.Exists)
            {
                return;
            }

            foreach (var d in di.EnumerateDirectories())
            {
                if (d.Name.StartsWith("v"))
                {
                    continue;
                }

                foreach (var v in d.EnumerateDirectories())
                {
                    var frameworkInfo = new FrameworkInformation();
                    var frameworkName = new FrameworkName(d.Name, new Version(v.Name.TrimStart('v')));
                    var facadePath = Path.Combine(v.FullName, "RuntimeFacades");
                    string redistList = Path.Combine(v.FullName, "RedistList", "FrameworkList.xml");

                    if (File.Exists(redistList))
                    {
                        foreach (var assemblyName in GetFrameworkAssemblies(redistList))
                        {
                            var assemblyPath = Path.Combine(v.FullName, assemblyName + ".dll");

                            if (!File.Exists(assemblyPath))
                            {
#if DESKTOP
                                // Check the gac fror full framework only
                                if (frameworkName.Identifier == VersionUtility.DefaultTargetFramework.Identifier)
                                {
                                    if (GlobalAssemblyCache.ResolvePartialName(assemblyName, out assemblyPath) == null)
                                    {
                                        continue;
                                    }
                                }
                                else
#endif
                                {
                                    continue;
                                }
                            }

                            frameworkInfo.Assemblies.Add(new AssemblyInfo(assemblyName, assemblyPath));
                        }
                    }
                    else
                    {
                        foreach (var assemblyFileInfo in v.EnumerateFiles("*.dll"))
                        {
                            try
                            {
#if DESKTOP // CORECLR_TODO: AssemblyName.GetAssemblyName
                                var an = AssemblyName.GetAssemblyName(assemblyFileInfo.FullName);
                                frameworkInfo.Assemblies.Add(new AssemblyInfo(an.Name, assemblyFileInfo.FullName));
#else
                                frameworkInfo.Assemblies.Add(new AssemblyInfo(assemblyFileInfo.Name, assemblyFileInfo.FullName));
#endif
                            }
                            catch (Exception ex)
                            {
                                // Probably not a valid assembly
                                Trace.TraceError(ex.Message);
                            }
                        }
                    }

                    cache[frameworkName] = frameworkInfo;
                }
            }
        }

        private static IEnumerable<string> GetFrameworkAssemblies(string path)
        {
            if (!File.Exists(path))
            {
                yield break;
            }

            using (var stream = File.OpenRead(path))
            {
                var frameworkList = XDocument.Load(stream);

                foreach (var e in frameworkList.Root.Elements())
                {
                    yield return e.Attribute("AssemblyName").Value;
                }
            }
        }

        private class FrameworkInformation
        {
            public FrameworkInformation()
            {
                Assemblies = new List<AssemblyInfo>();
            }

            public IList<AssemblyInfo> Assemblies { get; private set; }
        }

        private class AssemblyInfo
        {
            public AssemblyInfo(string name, string path)
            {
                Name = name;
                Path = path;
            }

            public string Name { get; private set; }

            public string Path { get; private set; }
        }
    }
}
