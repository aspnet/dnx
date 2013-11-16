using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;

namespace Loader
{
    public class FrameworkReferenceResolver : IFrameworkReferenceResolver
    {
        private static readonly ILookup<FrameworkName, string> _cache = PopulateCache();

        public IEnumerable<string> GetFrameworkReferences(FrameworkName frameworkName)
        {
            return _cache[frameworkName];
        }

        public IEnumerable<MetadataReference> GetDefaultReferences(FrameworkName frameworkName)
        {
            return new[] {
                MetadataReference.CreateAssemblyReference("mscorlib"),
                MetadataReference.CreateAssemblyReference("System"),
                MetadataReference.CreateAssemblyReference("System.Core"),
                MetadataReference.CreateAssemblyReference("Microsoft.CSharp")
            };
        }

        private static ILookup<FrameworkName, string> PopulateCache()
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
                         .Select(a => new { FrameworkName = new FrameworkName(d.Name, new Version(v.Name.TrimStart('v'))), Assembly = a })))
                     .ToLookup(a => a.FrameworkName, a => a.Assembly);
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
    }
}
