using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Roslyn.AssemblyNeutral;
using Microsoft.Net.Runtime.Services;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynProjectMetadata
    {
        private readonly CompilationContext _compilationContext;

        public RoslynProjectMetadata(CompilationContext compilationContext, IProjectResolver resolver)
        {
            _compilationContext = compilationContext;

            SourceFiles = _compilationContext.Compilation
                                             .SyntaxTrees
                                             .Select(t => t.FilePath)
                                             .Where(p => !String.IsNullOrEmpty(p)) // REVIEW: Raw sources?
                                             .ToList();

            RawReferences = new List<byte[]>();
            References = new List<string>();
            ProjectReferences = new List<string>();

            foreach (var r in _compilationContext.Compilation.References)
            {
                ProcessReference(r, resolver);
            }

#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
            var formatter = DiagnosticFormatter.Instance;
#else
            var formatter = new DiagnosticFormatter();
#endif
            Errors = _compilationContext.Compilation
                                        .GetDiagnostics()
                                        .Where(d => d.Severity == DiagnosticSeverity.Error)
                                        .Select(d => formatter.Format(d))
                                        .ToList();

            Warnings = _compilationContext.Compilation
                                        .GetDiagnostics()
                                        .Where(d => d.Severity == DiagnosticSeverity.Warning)
                                        .Select(d => formatter.Format(d))
                                        .ToList();
        }

        private void ProcessReference(MetadataReference reference, IProjectResolver resolver)
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
                Project project;
                if (resolver.TryResolveProject(compilationReference.Compilation.AssemblyName, out project))
                {
                    ProjectReferences.Add(project.ProjectFilePath);
                }
                else
                {
                    var stream = new MemoryStream();
                    var result = compilationReference.Compilation.EmitMetadataOnly(stream);

                    if (result.Success)
                    {
                        RawReferences.Add(stream.ToArray());
                    }
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

        public IList<string> Warnings
        {
            get;
            private set;
        }

        public IList<byte[]> RawReferences
        {
            get;
            private set;
        }

        public IList<string> ProjectReferences
        {
            get;
            private set;
        }
    }
}
