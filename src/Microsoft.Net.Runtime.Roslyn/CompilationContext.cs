using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class CompilationContext
    {
        /// <summary>
        /// The project associated with this compilation
        /// </summary>
        public Project Project { get; set; }

        // Processed information
        public CSharpCompilation Compilation { get; set; }
        public IList<Diagnostic> Diagnostics { get; set; }
        public IList<CompilationContext> ProjectReferences { get; set; }
        public IDictionary<string, AssemblyNeutralMetadataReference> AssemblyNeutralReferences { get; set; }

        public CompilationContext()
        {
            Diagnostics = new List<Diagnostic>();
            AssemblyNeutralReferences = new Dictionary<string, AssemblyNeutralMetadataReference>();
        }

        internal void PopulateAllAssemblyNeutralResources(IList<ResourceDescription> resources)
        {
            foreach (var reference in AssemblyNeutralReferences.Values)
            {
                resources.Add(new ResourceDescription(reference.Name + ".dll", () =>
                {
                    // REVIEW: Performance?
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
