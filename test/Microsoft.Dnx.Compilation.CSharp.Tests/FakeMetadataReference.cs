using Microsoft.CodeAnalysis;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    internal class FakeMetadataReference : IRoslynMetadataReference
    {
        public string Name { get; set; }
        public MetadataReference MetadataReference { get; set; }
    }
}