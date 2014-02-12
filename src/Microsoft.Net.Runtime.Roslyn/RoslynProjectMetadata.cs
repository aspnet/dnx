using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Net.Runtime.Roslyn.AssemblyNeutral;
using Microsoft.Net.Runtime.Services;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynProjectMetadata : IProjectMetadata
    {
        private readonly CompilationContext _compilationContext;

        public RoslynProjectMetadata(CompilationContext compilationContext)
        {
            _compilationContext = compilationContext;

            SourceFiles = _compilationContext.Compilation
                                             .SyntaxTrees
                                             .Select(t => t.FilePath)
                                             .ToList();

            RawReferences = new List<byte[]>();
            References = new List<string>();

            foreach (var r in _compilationContext.Compilation.References)
            {
                ProcessReference(r);
            }

#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
            var formatter = DiagnosticFormatter.Instance;
#else
            var formatter = new DiagnosticFormatter();
#endif
            Errors = _compilationContext.Compilation
                                        .GetDiagnostics()
                                        .Select(d => formatter.Format(d))
                                        .ToList();
        }

        private void ProcessReference(MetadataReference reference)
        {
            var fileReference = reference as MetadataFileReference;

            if (fileReference != null)
            {
                References.Add(fileReference.FullPath);
            }

            // Project reference
            var compilationReference = reference as CompilationReference;

            if (compilationReference != null)
            {
                var stream = new MemoryStream();
                var result = compilationReference.Compilation.EmitMetadataOnly(stream);

                if (result.Success)
                {
                    RawReferences.Add(stream.ToArray());
                }
            }

            var imageReference = reference as MetadataImageReference;

            if (imageReference != null)
            {

            }

        }

        public IList<string> SourceFiles
        {
            get;
            private set;
        }

        public IList<string> References
        {
            get;
            private set;
        }

        public IList<string> Errors
        {
            get;
            private set;
        }


        public IList<byte[]> RawReferences
        {
            get;
            private set;
        }
    }
}
