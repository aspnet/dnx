using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn
{
    internal static class MetadataFileReferenceFactory
    {
        internal static MetadataReference CreateReference(string path)
        {
#if NET45
            return new MetadataFileReference(path);
#else
            using (var stream = File.OpenRead(path))
            {
                return new MetadataImageReference(stream);
            }
#endif
        }
    }
}