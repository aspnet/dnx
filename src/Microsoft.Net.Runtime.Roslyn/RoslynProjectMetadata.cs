using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

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

            RawReferences = context.MetadataReferences.OfType<AssemblyNeutralMetadataReference>().Select(r =>
            {
                var ms = new MemoryStream();
                r.OutputStream.CopyTo(ms);

                return new
                {
                    Name = r.Name,
                    Bytes = ms.ToArray()
                };
            })
            .ToDictionary(a => a.Name, a => a.Bytes);

#if NET45
            References = context.Compilation.References.OfType<MetadataFileReference>()
                                        .Select(r => r.FullPath)
                                        .ToList();
#else
            References = context.MetadataReferences.OfType<IMetadataFileReference>()
                                .Select(r => r.Path)
                                .ToList();
#endif

            ProjectReferences = context.ProjectReferences.Select(p => p.Project.ProjectFilePath)
                                                         .ToList();

            var formatter = new DiagnosticFormatter();

            var diagnostics = context.Compilation.GetDiagnostics()
                .Concat(context.Diagnostics)
                .ToList();

            Errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error || d.IsWarningAsError)
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

        public IDictionary<string, byte[]> RawReferences
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
