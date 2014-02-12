using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn.AssemblyNeutral
{
    public class AssemblyNeutralMetadataReference : MetadataReferenceWrapper
    {
        public AssemblyNeutralMetadataReference(TypeCompilationContext context)
            : base(context.RealOrShallowReference())
        {
            Name = context.AssemblyName;
            OutputStream = context.OutputStream;
        }

        public string Name { get; private set; }

        public Stream OutputStream { get; private set; }
    }
}
