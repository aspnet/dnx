using System.Collections.Generic;
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
        public IList<AssemblyNeutralMetadataReference> AssemblyNeutralReferences { get; set; }
        public IList<CompilationContext> ProjectReferences { get; set; }

        public CompilationContext()
        {
            Diagnostics = new List<Diagnostic>();
        }

        internal void PopulateAllAssemblyNeutralResources(IList<ResourceDescription> resources)
        {
            PopulateAllAssemblyNeutralResources(resources, new HashSet<string>());
        }

        internal void PopulateAllAssemblyNeutralResources(IList<ResourceDescription> resources, HashSet<string> used)
        {
            // TODO: Only embed things that are used (we need roslyn's help to know this)
            // This will over embed right now since it's embedding transitively and we don't want that
            foreach (var reference in AssemblyNeutralReferences)
            {
                if (used.Contains(reference.Name))
                {
                    continue;
                }

                resources.Add(new ResourceDescription(reference.Name + ".dll", () => reference.OutputStream, isPublic: true));
                used.Add(reference.Name);
            }

            foreach (var projectContext in ProjectReferences)
            {
                projectContext.PopulateAllAssemblyNeutralResources(resources, used);
            }
        }
    }
}
