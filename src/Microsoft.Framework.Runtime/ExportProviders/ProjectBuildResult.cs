using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    public struct ProjectBuildResult : IProjectBuildResult
    {
        private readonly bool _success;
        private readonly IEnumerable<string> _warnings;
        private readonly IEnumerable<string> _errors;

        public ProjectBuildResult(bool success, IEnumerable<string> warnings, IEnumerable<string> errors)
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