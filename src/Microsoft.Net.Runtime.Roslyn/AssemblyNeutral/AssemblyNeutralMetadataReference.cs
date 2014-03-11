using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class AssemblyNeutralMetadataReference : RoslynMetadataReference
    {
        public AssemblyNeutralMetadataReference(TypeCompilationContext context)
            : base(context.AssemblyName, context.RealOrShallowReference())
        {
            OutputStream = context.OutputStream;
        }

        public Stream OutputStream { get; private set; }
    }
}
