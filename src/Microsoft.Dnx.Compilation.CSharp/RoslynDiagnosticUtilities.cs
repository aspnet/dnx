using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.Dnx.Compilation.CSharp
{
    /// <summary>
    /// Summary description for RoslynDiagnosticUtilities
    /// </summary>
    internal class RoslynDiagnosticUtilities
    {
        internal static IEnumerable<string> Convert(IEnumerable<Diagnostic> diagnostics)
        {
            var formatter = new DiagnosticFormatter();

            return diagnostics.Select(d => formatter.Format(d)).ToList();
        }

        internal static bool IsError(Diagnostic diagnostic)
        {
            return diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.IsWarningAsError;
        }
    }
}