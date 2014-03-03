using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Net.Runtime.Loader;

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynProjectMetadata
    {
        public RoslynProjectMetadata(CompilationContext context)
        {
            SourceFiles = context.Compilation
                                 .SyntaxTrees
                                 .Select(t => t.FilePath)
                                 .Where(p => !String.IsNullOrEmpty(p)) // REVIEW: Raw sources?
                                 .ToList();

            RawReferences = context.AssemblyNeutralReferences.Select(r =>
            {
                var ms = new MemoryStream();
                r.Value.OutputStream.CopyTo(ms);
                return ms.ToArray();
            })
            .ToList();

            References = context.Exports.OfType<IMetadataFileReference>().Select(r => r.Path)
                                                                         .ToList();

            ProjectReferences = context.ProjectReferences.Select(p => p.Project.ProjectFilePath)
                                                                    .ToList();

            var formatter = new DiagnosticFormatter();

            var diagnostics = context.Compilation.GetDiagnostics()
                .Concat(context.Diagnostics)
                .ToList();

            Errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error)
                                .Select(d => formatter.Format(d))
                                .ToList();

            Warnings = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning)
                                  .Select(d => formatter.Format(d))
                                  .ToList();
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
