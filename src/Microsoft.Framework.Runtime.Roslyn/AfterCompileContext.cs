using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Framework.Runtime.Roslyn
{
    internal class AfterCompileContext : IAfterCompileContext
    {
        private readonly CompilationContext _context;

        public AfterCompileContext(CompilationContext context, IEnumerable<Diagnostic> emitDiagnostics)
        {
            _context = context;
            var diagnostics = new List<Diagnostic>(context.Diagnostics);
            diagnostics.AddRange(emitDiagnostics);

            Diagnostics = diagnostics;
        }

        public IProjectContext ProjectContext
        {
            get
            {
                return _context.ProjectContext;
            }
        }

        public Stream AssemblyStream { get; set; }

        public Stream SymbolStream { get; set; }

        public Stream XmlDocStream { get; set; }

        public CSharpCompilation Compilation
        {
            get
            {
                return _context.Compilation;
            }

            set
            {
                _context.Compilation = value;
            }
        }

        public IList<Diagnostic> Diagnostics { get; }
    }
}