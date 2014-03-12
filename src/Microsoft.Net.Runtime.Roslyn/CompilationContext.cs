using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class CompilationContext
    {
        private RoslynLibraryExport _roslynLibraryExport;

        /// <summary>
        /// The project associated with this compilation
        /// </summary>
        public Project Project { get; private set; }

        // Processed information
        public CSharpCompilation Compilation { get; private set; }
        public IList<Diagnostic> Diagnostics { get; private set; }

        public IList<IMetadataReference> MetadataReferences { get; private set; }
        public IList<CompilationContext> ProjectReferences { get; private set; }

        public CompilationContext(CSharpCompilation compilation,
                                  IList<IMetadataReference> metadataReferences,
                                  IList<CompilationContext> projectReferences,
                                  IList<Diagnostic> diagnostics,
                                  Project project)
        {
            Compilation = compilation;
            MetadataReferences = metadataReferences;
            ProjectReferences = projectReferences;
            Diagnostics = diagnostics;
            Project = project;
        }

        public RoslynLibraryExport GetLibraryExport()
        {
            if (_roslynLibraryExport == null)
            {
                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();

                // Compilation reference
                var metadataReference = Compilation.ToMetadataReference(embedInteropTypes: Project.EmbedInteropTypes);
                metadataReferences.Add(new RoslynMetadataReference(Project.Name, metadataReference));

                // Other references
                metadataReferences.AddRange(MetadataReferences);

                // Shared sources
                foreach (var sharedFile in Project.SharedFiles)
                {
                    sourceReferences.Add(new SourceFileReference(sharedFile));
                }

                _roslynLibraryExport = new RoslynLibraryExport(metadataReferences, sourceReferences, this);
            }

            return _roslynLibraryExport;
        }

        public void PopulateAssemblyNeutralResources(IList<ResourceDescription> resources)
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
