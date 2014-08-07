using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.Runtime
{
    public struct DiagnosticResult : IDiagnosticResult
    {
        public static readonly DiagnosticResult Successful = new DiagnosticResult(success: true, warnings: Enumerable.Empty<string>(), errors: Enumerable.Empty<string>());

        private readonly bool _success;
        private readonly IEnumerable<string> _warnings;
        private readonly IEnumerable<string> _errors;

        public DiagnosticResult(bool success, IEnumerable<string> warnings, IEnumerable<string> errors)
        {
            _success = success;
            _warnings = warnings;
            _errors = errors;
        }

        public bool Success
        {
            get
            {
                return _success;
            }
        }

        public IEnumerable<string> Warnings
        {
            get
            {
                return _warnings;
            }
        }

        public IEnumerable<string> Errors
        {
            get
            {
                return _errors;
            }
        }
    }
}