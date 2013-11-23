using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            // Don't do anything special for desktop
            if (frameworkName.Identifier == VersionUtility.DefaultTargetFramework.Identifier)
            {
                return new[] {
                    MetadataReference.CreateAssemblyReference("mscorlib"),
                    MetadataReference.CreateAssemblyReference("System"),
                    MetadataReference.CreateAssemblyReference("System.Core"),
                    MetadataReference.CreateAssemblyReference("Microsoft.CSharp")
                };
            }

            FrameworkInformation frameworkInfo;
            if (_cache.TryGetValue(frameworkName, out frameworkInfo))
            {
                return frameworkInfo.Assemblies.Select(a => new MetadataFileReference(a.Path));
            }

            return Enumerable.Empty<MetadataReference>();
        }

        public string GetRuntimeFacadePath(FrameworkName frameworkName)
        {
            FrameworkInformation frameworkInfo;
            if (_cache.TryGetValue(frameworkName, out frameworkInfo))
            {
                return frameworkInfo.FacadePath;
            }

            return null;
        }

        private static IDictionary<FrameworkName, FrameworkInformation> PopulateCache()
        {
            var info = new Dictionary<FrameworkName, FrameworkInformation>();

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

            return info;
        }

        private static void PopulateReferenceAssemblies(string path, IDictionary<FrameworkName, FrameworkInformation> cache)
        {
            var di = new DirectoryInfo(path);

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

                    frameworkInfo.FacadePath = Directory.Exists(facadePath) ? facadePath : null;

                    foreach (var a in GetFrameworkAssemblies(redistList))
                    {
                        var assemblyPath = Path.Combine(v.FullName, a + ".dll");
                        frameworkInfo.Assemblies.Add(new AssemblyInfo(a, assemblyPath));
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

            public string FacadePath { get; set; }
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
