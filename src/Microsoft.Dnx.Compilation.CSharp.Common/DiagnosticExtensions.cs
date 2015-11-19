// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Dnx.Compilation.CSharp;

namespace Microsoft.CodeAnalysis
{
    public static class DiagnosticExtensions
    {
        public static DiagnosticMessage ToDiagnosticMessage(this Diagnostic self, FrameworkName targetFramework)
        {
            var mappedLineSpan = self.Location.GetMappedLineSpan();
            return new DiagnosticMessage(
                self.Id,
                self.GetMessage(),
                RoslynDiagnosticFormatter.Format(self, targetFramework),
                mappedLineSpan.Path,
                ConvertSeverity(self.Severity),
                mappedLineSpan.StartLinePosition.Line + 1,
                mappedLineSpan.StartLinePosition.Character + 1,
                mappedLineSpan.EndLinePosition.Line + 1,
                mappedLineSpan.EndLinePosition.Character + 1);
        }

        public static CompilationFailure ToCompilationFailure(this IEnumerable<Diagnostic> self, FrameworkName targetFramework)
        {
            var diagnostic = self.FirstOrDefault();
            if (diagnostic == null)
            {
                throw new ArgumentException("At least one diagnostic must be present.", nameof(diagnostic));
            }

            return new CompilationFailure(
                diagnostic.Location.GetMappedLineSpan().Path,
                self.Select(d => d.ToDiagnosticMessage(targetFramework)));
        }

        private static DiagnosticMessageSeverity ConvertSeverity(DiagnosticSeverity severity)
        {
            switch (severity)
            {
                case DiagnosticSeverity.Warning:
                    return DiagnosticMessageSeverity.Warning;
                case DiagnosticSeverity.Error:
                    return DiagnosticMessageSeverity.Error;
                default:
                    return DiagnosticMessageSeverity.Info;
            }
        }
    }
}
