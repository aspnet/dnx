using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    public interface IDiagnosticResult
    {
        bool Success { get; }

        IEnumerable<ICompilationMessage> Diagnostics { get; }
    }
}