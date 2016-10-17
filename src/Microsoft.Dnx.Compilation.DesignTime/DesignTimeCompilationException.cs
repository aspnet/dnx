// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Compilation.DesignTime
{
    internal class DesignTimeCompilationException : Exception, ICompilationException
    {
        public DesignTimeCompilationException(IList<DiagnosticMessage> compileResponseErrors)
            : base(string.Join(Environment.NewLine, compileResponseErrors.Select(e => e.FormattedMessage)))
        {
            CompilationFailures = compileResponseErrors.GroupBy(g => g.SourceFilePath, StringComparer.OrdinalIgnoreCase)
                                                       .Select(g => new CompilationFailure(g.Key, g))
                                                       .ToArray();
        }

        public IEnumerable<CompilationFailure> CompilationFailures { get; }
    }
}