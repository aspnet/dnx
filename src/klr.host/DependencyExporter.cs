using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Net.Runtime;

namespace klr.host
{
    // REVIEW: Make this work differently.
    public class DependencyExporter : ILibraryExportProvider
    {
        private readonly string[] _searchPaths;

        public DependencyExporter(string[] searchPaths)
        {
            _searchPaths = searchPaths;
        }

        public ILibraryExport GetLibraryExport(string name, FrameworkName targetFramework)
        {
            foreach (var searchPath in _searchPaths)
            {
                var file = Path.Combine(searchPath, name + ".dll");

                if (File.Exists(file))
                {
                    return new DependencyExport(file);
                }
            }

            return null;
        }

        private class DependencyExport : ILibraryExport
        {
            public DependencyExport(string file)
            {
                MetadataReferences = new List<IMetadataReference>();
                SourceReferences = new List<ISourceReference>();
                MetadataReferences.Add(new MetadataFileReference(file));
            }

            public IList<IMetadataReference> MetadataReferences
            {
                get;
                private set;
            }

            public IList<ISourceReference> SourceReferences
            {
                get;
                private set;
            }
        }
    }
}
