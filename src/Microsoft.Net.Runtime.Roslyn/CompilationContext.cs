using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public IList<IMetadataReference> MetadataReferences { get; set; }
        public IList<CompilationContext> ProjectReferences { get; set; }


        public CompilationContext()
        {
            Diagnostics = new List<Diagnostic>();
        }

        internal void PopulateAllAssemblyNeutralResources(IList<ResourceDescription> resources)
        {
            foreach (var reference in MetadataReferences.OfType<AssemblyNeutralMetadataReference>())
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
