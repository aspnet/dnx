using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.Framework.Runtime.Roslyn
{
    public class RoslynCompilationException : CompilationException
    {
        public RoslynCompilationException(IEnumerable<Diagnostic> diagnostics) :
            base(ConvertToErrors(diagnostics))
        {
            Diagnostics = diagnostics;
        }

        public IEnumerable<Diagnostic> Diagnostics { get; private set; }

        private static IList<string> ConvertToErrors(IEnumerable<Diagnostic> diagnostics)
        {
            return RoslynDiagnosticUtilities.Convert(diagnostics.Where(RoslynDiagnosticUtilities.IsError)).ToList();
        }
    }
}