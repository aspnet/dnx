using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class EmbeddedMetadataReference : RoslynMetadataReference
    {
        public EmbeddedMetadataReference(TypeCompilationContext context)
            : base(context.AssemblyName, context.RealOrShallowReference())
        {
            OutputStream = context.OutputStream;
        }

        public EmbeddedMetadataReference(string name, Stream stream)
            : base(name, new MetadataImageReference(stream))
        {
            OutputStream = stream;
        }

        public Stream OutputStream { get; private set; }
    }
}
