using System.Collections.Generic;

namespace Microsoft.Framework.Runtime.Compilation
{
    public interface IDiagnosticResult
    {
        bool Success { get; }

        IEnumerable<ICompilationMessage> Diagnostics { get; }
    }
}