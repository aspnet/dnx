// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Framework.Runtime
{
    internal class DesignTimeCompilationException : Exception, ICompilationException
    {
        public DesignTimeCompilationException(IList<CompilationMessage> compileResponseErrors)
            : base(string.Join(Environment.NewLine, compileResponseErrors.Select(e => e.FormattedMessage)))
        {
            CompilationFailures = compileResponseErrors.GroupBy(g => g.SourceFilePath, StringComparer.OrdinalIgnoreCase)
                                                       .Select(g => new CompilationFailure
                                                       {
                                                           SourceFilePath = g.Key,
                                                           Messages = g
                                                       })
                                                       .ToArray();
        }

        public IEnumerable<ICompilationFailure> CompilationFailures { get; }
    }
}