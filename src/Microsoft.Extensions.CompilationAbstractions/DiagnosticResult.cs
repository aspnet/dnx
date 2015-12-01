using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Extensions.CompilationAbstractions
{
    public class DiagnosticResult
    {
        public static readonly DiagnosticResult Successful = new DiagnosticResult(success: true, diagnostics: Enumerable.Empty<DiagnosticMessage>());

        public bool Success { get; }

        public IEnumerable<DiagnosticMessage> Diagnostics { get; }

        public DiagnosticResult(bool success, IEnumerable<DiagnosticMessage> diagnostics)
        {
            Success = success;
            Diagnostics = diagnostics;
        }
    }
}