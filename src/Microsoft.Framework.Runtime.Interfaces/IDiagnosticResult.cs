using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IDiagnosticResult
    {
        bool Success { get; }

        IEnumerable<ICompilationMessage> Diagnostics { get; }
    }
}