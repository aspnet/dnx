using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using NuGet;

namespace Loader
{
    public class FrameworkReferenceResolver : IFrameworkReferenceResolver
    {
        private static readonly ILookup<FrameworkName, AssemblyInfo> _cache = PopulateCache();

        public IEnumerable<string> GetFrameworkReferences(FrameworkName frameworkName)
        {
            return _cache[frameworkName].Select(a => a.Name);
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

            return _cache[frameworkName].Select(a => GetMetadataReference(a));
        }

        private MetadataReference GetMetadataReference(AssemblyInfo a)
        {
            if (a.Path != null)
            {
                return new MetadataFileReference(a.Path);
            }

            return MetadataReference.CreateAssemblyReference(a.Name);
        }

        private static ILookup<FrameworkName, AssemblyInfo> PopulateCache()
        {
            IEnumerable<Tuple<FrameworkName, string, string>> referenceAssemblies = GetDefaultReferenceAssemblies();

            // Additional profile paths
            var profilePaths = Environment.GetEnvironmentVariable("PROFILE_PATHS");

            if (profilePaths != null)
            {
                referenceAssemblies = referenceAssemblies.Concat(
                    profilePaths.Split(';')
                                .SelectMany(profilePath => GetReferenceAssembliesFromCustomPath(profilePath)));
            }

            return referenceAssemblies.ToLookup(t => t.Item1, t => new AssemblyInfo(t.Item2, t.Item3));
        }

        private static IEnumerable<Tuple<FrameworkName, string, string>> GetReferenceAssembliesFromCustomPath(string profilePath)
        {
            var di = new DirectoryInfo(profilePath);
            return di.EnumerateDirectories()
                     .SelectMany(d => d.EnumerateDirectories()
                                       .SelectMany(v => v.EnumerateFiles("*.dll")
                                           .Select(fi => Tuple.Create(new FrameworkName(d.Name, new Version(v.Name.TrimStart('v'))), AssemblyName.GetAssemblyName(fi.FullName).Name, fi.FullName))));

        }

        private static IEnumerable<Tuple<FrameworkName, string, string>> GetDefaultReferenceAssemblies()
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Reference Assemblies\Microsoft\Framework\");

            var di = new DirectoryInfo(path);

            // { 
            //    Name = .NETFramework,
            //    Versions = [ { Version = 4.5, Assemblies = [...] } ] 
            // }

            return di.EnumerateDirectories()
                     .SelectMany(d => d.EnumerateDirectories()
                                       .SelectMany(v => GetFrameworkAssemblies(Path.Combine(v.FullName, "RedistList", "FrameworkList.xml"))
                                           .Select(a => Tuple.Create(new FrameworkName(d.Name, new Version(v.Name.TrimStart('v'))), a, (string)null))));

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

        private class AssemblyInfo
        {
            public AssemblyInfo(string name, string path)
            {
                Name = name;
                Path = path;
            }

            public string Name { get; private set; }

            public string Path { get; set; }
        }
    }
}
