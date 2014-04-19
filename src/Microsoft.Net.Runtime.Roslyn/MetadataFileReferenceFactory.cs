using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn
{
    internal static class MetadataFileReferenceFactory
    {
        internal static MetadataReference CreateReference(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                return new MetadataImageReference(stream);
            }
        }
    }
}