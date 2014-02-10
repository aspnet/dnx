using Microsoft.CodeAnalysis;

namespace Microsoft.Net.Runtime.Roslyn.AssemblyNeutral
{
    public class AssemblyNeutralMetadataReference : MetadataReferenceWrapper
    {
        public AssemblyNeutralMetadataReference(string name, MetadataReference reference)
            : base(reference)
        {
            Name = name;
        }

        public string Name { get; set; }
    }
}
