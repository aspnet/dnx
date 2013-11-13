using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using NuGet;

namespace Loader
{
    public class FrameworkReferenceResolver : IFrameworkReferenceResolver
    {
        private static readonly MetadataReference[] _defaultReferences = new[]{
            new MetadataFileReference(typeof(object).Assembly.Location),
                MetadataFileReference.CreateAssemblyReference("System"),
                MetadataFileReference.CreateAssemblyReference("System.Core"),
                MetadataFileReference.CreateAssemblyReference("Microsoft.CSharp")
        };

        public IEnumerable<MetadataReference> GetFrameworkReferences(string frameworkName)
        {
            var name = VersionUtility.ParseFrameworkName(frameworkName);

            if (name != VersionUtility.UnsupportedFrameworkName)
            {
                return _defaultReferences;
            }

            try
            {
                // Probably a path so try to enumerate
                return GetAssemblies(frameworkName).ToList();
            }
            catch
            {
                // Return the default
                return _defaultReferences;
            }
        }

        private IEnumerable<MetadataReference> GetAssemblies(string path)
        {
            foreach (var file in Directory.EnumerateFiles(path, "*.dll"))
            {
                yield return new MetadataFileReference(file);
            }


        }
    }
}
