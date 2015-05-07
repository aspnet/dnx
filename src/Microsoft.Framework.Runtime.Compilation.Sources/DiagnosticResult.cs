using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.Runtime.Compilation
{
    internal struct DiagnosticResult : IDiagnosticResult
    {
        public static readonly DiagnosticResult Successful = new DiagnosticResult(success: true,
                                                                                  diagnostics: Enumerable.Empty<ICompilationMessage>());

        public DiagnosticResult(bool success, IEnumerable<ICompilationMessage> diagnostics)
        {
            Success = success;
            Diagnostics = diagnostics.ToList();
        }

        public bool Success { get; }

        public IEnumerable<ICompilationMessage> Diagnostics { get; }
    }
}