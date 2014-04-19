using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn
{
    internal class MetadataFileReferenceFactory
    {
        private readonly Dictionary<string, MetadataReference> _metadataCache = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        public MetadataReference GetMetadataReference(string path)
        {
            MetadataReference metadata;
            if (!_metadataCache.TryGetValue(path, out metadata))
            {
                using (var stream = File.OpenRead(path))
                {
                    metadata = new MetadataImageReference(stream);
                    _metadataCache[path] = metadata;
                }
            }
            return metadata;
        }
    }
}