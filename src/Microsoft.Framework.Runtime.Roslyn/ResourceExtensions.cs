using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    internal static class ResourceExtensions
    {
        public static void AddEmbeddedReferences(this IList<ResourceDescription> resources, IEnumerable<EmbeddedMetadataReference> references)
        {
            foreach (var reference in references)
            {
                resources.Add(new ResourceDescription(reference.Name + ".dll", () =>
                {
                    return new MemoryStream(reference.Contents);
                }, 
                isPublic: true));
            }
        }
    }
}
