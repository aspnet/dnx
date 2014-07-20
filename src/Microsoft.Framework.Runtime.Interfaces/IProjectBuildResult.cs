using System;
using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IProjectBuildResult
    {
        bool Success { get; }

        IEnumerable<string> Warnings { get; }

        IEnumerable<string> Errors { get; }
    }
}