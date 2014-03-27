using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn
{
    internal static class ResourceExtensions
    {
        public static void AddEmbeddedReferences(this IList<ResourceDescription> resources, IEnumerable<EmbeddedMetadataReference> references)
        {
            foreach (var reference in references)
            {
                resources.Add(new ResourceDescription(reference.Name + ".dll", () =>
                {
                    var ms = new MemoryStream();
                    reference.OutputStream.Position = 0;
                    reference.OutputStream.CopyTo(ms);
                    ms.Position = 0;
                    return ms;

                }, isPublic: true));
            }
        }
    }
}
